using System.Net.Http.Json;

namespace Mastemis.Client.Core.Networking.Http;

public sealed record ServerProbeResult(bool IsAvailable, bool IsReady, string? Version, string? Error);

public interface IServerProbe
{
    Task<ServerProbeResult> ProbeAsync(Uri baseUri, CancellationToken cancellationToken);
}

public sealed class ServerProbe(IHttpClientFactory clients) : IServerProbe
{
    public async Task<ServerProbeResult> ProbeAsync(Uri baseUri, CancellationToken cancellationToken)
    {
        try
        {
            using var client = clients.CreateClient("Mastemis.Probe");
            using var live = await client.GetAsync(new Uri(baseUri, "/health/live"), cancellationToken).ConfigureAwait(false);
            using var ready = await client.GetAsync(new Uri(baseUri, "/health/ready"), cancellationToken).ConfigureAwait(false);
            string? version = null;
            try
            {
                var document = await client.GetFromJsonAsync<Dictionary<string, string>>(new Uri(baseUri, "/api/system/version"), cancellationToken).ConfigureAwait(false);
                document?.TryGetValue("version", out version);
            }
            catch (HttpRequestException) { }
            return new(live.IsSuccessStatusCode, ready.IsSuccessStatusCode, version, null);
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException)
        {
            return new(false, false, null, error is TaskCanceledException ? "Connection timed out." : "Server is unavailable.");
        }
    }
}
