using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mastemis.Client.Core.Session;

namespace Mastemis.Client.Core.Networking.Http;

public interface IApiTransport
{
    Task<T?> GetAsync<T>(string path, CancellationToken cancellationToken);
    Task<TResponse?> SendAsync<TRequest, TResponse>(HttpMethod method, string path, TRequest body, string? idempotencyKey, CancellationToken cancellationToken);
    Task SendAsync<TRequest>(HttpMethod method, string path, TRequest body, string? idempotencyKey, CancellationToken cancellationToken);
    Task<Stream> DownloadAsync(string path, CancellationToken cancellationToken);
    Task<TResponse?> UploadAsync<TResponse>(HttpMethod method, string path, Stream content, string contentType, string? idempotencyKey, CancellationToken cancellationToken);
}

public sealed class ApiTransport(HttpClient client, ClientSession session) : IApiTransport
{
    private const int MaximumProblemBytes = 64 * 1024;
    private string? antiforgeryToken;

    public async Task<T?> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, path);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken).ConfigureAwait(false);
    }

    public async Task<TResponse?> SendAsync<TRequest, TResponse>(HttpMethod method, string path, TRequest body, string? idempotencyKey, CancellationToken cancellationToken)
    {
        using var response = await SendCoreAsync(method, path, body, idempotencyKey, cancellationToken).ConfigureAwait(false);
        return response.StatusCode == HttpStatusCode.NoContent ? default :
            await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken).ConfigureAwait(false);
    }

    public async Task SendAsync<TRequest>(HttpMethod method, string path, TRequest body, string? idempotencyKey, CancellationToken cancellationToken)
        => (await SendCoreAsync(method, path, body, idempotencyKey, cancellationToken).ConfigureAwait(false)).Dispose();

    public async Task<Stream> DownloadAsync(string path, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, path);
        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
            return await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        }
        catch { response.Dispose(); throw; }
    }

    public async Task<TResponse?> UploadAsync<TResponse>(HttpMethod method, string path, Stream content, string contentType, string? idempotencyKey, CancellationToken cancellationToken)
    {
        await EnsureAntiforgeryAsync(cancellationToken).ConfigureAwait(false);
        using var request = CreateRequest(method, path);
        request.Content = new StreamContent(content);
        request.Content.Headers.ContentType = new(contentType);
        request.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", antiforgeryToken);
        if (!string.IsNullOrWhiteSpace(idempotencyKey)) request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendCoreAsync<T>(HttpMethod method, string path, T body, string? idempotencyKey, CancellationToken cancellationToken)
    {
        await EnsureAntiforgeryAsync(cancellationToken).ConfigureAwait(false);
        using var request = CreateRequest(method, path);
        request.Content = JsonContent.Create(body);
        request.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", antiforgeryToken);
        if (!string.IsNullOrWhiteSpace(idempotencyKey)) request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        try { await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false); return response; }
        catch { response.Dispose(); throw; }
    }

    private async Task EnsureAntiforgeryAsync(CancellationToken cancellationToken)
    {
        if (antiforgeryToken is not null) return;
        using var request = CreateRequest(HttpMethod.Get, "/api/auth/antiforgery");
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        var value = await response.Content.ReadFromJsonAsync<AntiforgeryResponse>(cancellationToken).ConfigureAwait(false);
        antiforgeryToken = value?.Token ?? throw new InvalidDataException("The server did not return an antiforgery token.");
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
        => new(method, new Uri(session.ServerBaseUri ?? throw new InvalidOperationException("No Mastemis server is selected."), path));

    private async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        if (response.StatusCode == HttpStatusCode.Unauthorized) session.SignOut();
        ApiProblem problem;
        try
        {
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            if (bytes.Length > MaximumProblemBytes) throw new InvalidDataException();
            using var document = JsonDocument.Parse(bytes);
            var root = document.RootElement;
            problem = new(response.StatusCode, Read(root, "title") ?? "Request failed", Read(root, "detail"), Read(root, "code"),
                response.Headers.TryGetValues("X-Correlation-ID", out var values) ? values.FirstOrDefault() : null,
                new Dictionary<string, string[]>());
        }
        catch (Exception error) when (error is JsonException or InvalidDataException)
        {
            problem = new(response.StatusCode, "Request failed", null, null, null, new Dictionary<string, string[]>());
        }
        throw new ApiException(problem);
    }

    private static string? Read(JsonElement value, string property) => value.TryGetProperty(property, out var found) ? found.GetString() : null;
    private sealed record AntiforgeryResponse(string Token);
}
