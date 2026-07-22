using System.Net;
using System.Net.Http.Json;
using Mastemis.Contracts.Problems.ReferenceOutputs;
using Mastemis.Judge.Configuration;

namespace Mastemis.Judge.Worker.ReferenceOutputs;

public sealed class ReferenceOutputServerClient(HttpClient client, JudgeWorkerOptions options) : IReferenceOutputServerClient
{
    public async Task<ReferenceOutputJobLease?> ClaimAsync(int leaseSeconds, CancellationToken ct)
    {
        using var response = await SendJsonAsync(HttpMethod.Post, "api/worker/reference-jobs/claim", new { leaseSeconds }, ct);
        if (response.StatusCode == HttpStatusCode.NoContent) return null;
        await EnsureAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<ReferenceOutputJobLease>(cancellationToken: ct);
    }
    public Task<ReferenceOutputJobPayload> GetPayloadAsync(Guid jobId, Guid leaseToken, CancellationToken ct) =>
        GetJsonAsync<ReferenceOutputJobPayload>($"api/worker/reference-jobs/{jobId:D}/contract?leaseToken={leaseToken:D}", ct);
    public Task<byte[]> GetSourceAsync(Guid jobId, Guid leaseToken, string fileName, long maximumBytes, CancellationToken ct) =>
        GetBytesAsync($"api/worker/reference-jobs/{jobId:D}/sources/{Uri.EscapeDataString(fileName)}?leaseToken={leaseToken:D}", maximumBytes, ct);
    public Task<byte[]> GetInputAsync(Guid jobId, Guid leaseToken, int testIndex, long maximumBytes, CancellationToken ct) =>
        GetBytesAsync($"api/worker/reference-jobs/{jobId:D}/tests/{testIndex}/input?leaseToken={leaseToken:D}", maximumBytes, ct);
    public Task StartAsync(Guid jobId, Guid leaseToken, CancellationToken ct) => SendAsync(HttpMethod.Post,
        $"api/worker/reference-jobs/{jobId:D}/start", new { leaseToken, leaseSeconds = 60 }, ct);
    public Task RenewAsync(Guid jobId, Guid leaseToken, int leaseSeconds, CancellationToken ct) => SendAsync(HttpMethod.Post,
        $"api/worker/reference-jobs/{jobId:D}/renew", new { leaseToken, leaseSeconds }, ct);
    public async Task UploadAsync(Guid jobId, ReferenceOutputUploadMetadata metadata, ReadOnlyMemory<byte> output, CancellationToken ct)
    {
        var query = $"operationId={metadata.OperationId:D}&leaseToken={metadata.LeaseToken:D}&contractVersion={metadata.ContractVersion}&sha256={metadata.Sha256}&length={metadata.Length}&executionMilliseconds={metadata.ExecutionMilliseconds}&peakMemoryBytes={metadata.PeakMemoryBytes}&sandboxBackend={Uri.EscapeDataString(metadata.SandboxBackend)}&judgeVersion={Uri.EscapeDataString(metadata.JudgeVersion)}";
        using var request = Create(HttpMethod.Post, $"api/worker/reference-jobs/{jobId:D}/tests/{metadata.TestIndex}/output?{query}");
        request.Content = new ByteArrayContent(output.ToArray()); request.Content.Headers.ContentType = new("application/octet-stream");
        using var response = await client.SendAsync(request, ct); await EnsureAsync(response, ct);
    }
    public Task CompleteAsync(Guid jobId, ReferenceOutputCompletion completion, CancellationToken ct) =>
        SendAsync(HttpMethod.Post, $"api/worker/reference-jobs/{jobId:D}/complete", new { completion.OperationId, completion.LeaseToken, completion.CompletedTests, completion.JudgeVersion, completion.SandboxBackend }, ct);
    public Task FailAsync(Guid jobId, ReferenceOutputFailure failure, CancellationToken ct) =>
        SendAsync(HttpMethod.Post, $"api/worker/reference-jobs/{jobId:D}/fail", new { failure.OperationId, failure.LeaseToken, failure.FailureCode, failure.DiagnosticSummary }, ct);
    private async Task<T> GetJsonAsync<T>(string path, CancellationToken ct) { using var request = Create(HttpMethod.Get, path); using var response = await client.SendAsync(request, ct); await EnsureAsync(response, ct); return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct) ?? throw Rejected(); }
    private async Task<byte[]> GetBytesAsync(string path, long maximum, CancellationToken ct) { using var request = Create(HttpMethod.Get, path); using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct); await EnsureAsync(response, ct); if (response.Content.Headers.ContentLength > maximum) throw Rejected(); await using var stream = await response.Content.ReadAsStreamAsync(ct); using var output = new MemoryStream(); var buffer = new byte[81920]; long length = 0; while (true) { var read = await stream.ReadAsync(buffer, ct); if (read == 0) return output.ToArray(); length += read; if (length > maximum) throw Rejected(); await output.WriteAsync(buffer.AsMemory(0, read), ct); } }
    private async Task SendAsync<T>(HttpMethod method, string path, T value, CancellationToken ct) { using var response = await SendJsonAsync(method, path, value, ct); await EnsureAsync(response, ct); }
    private async Task<HttpResponseMessage> SendJsonAsync<T>(HttpMethod method, string path, T value, CancellationToken ct) { using var request = Create(method, path); request.Content = JsonContent.Create(value); return await client.SendAsync(request, ct); }
    private HttpRequestMessage Create(HttpMethod method, string path) { var request = new HttpRequestMessage(method, new Uri(options.ServerUrl, path)); request.Headers.TryAddWithoutValidation("Authorization", $"Worker {options.WorkerId.Value:D}.{options.Secret}"); return request; }
    private static Task EnsureAsync(HttpResponseMessage response, CancellationToken ct) { ct.ThrowIfCancellationRequested(); if (response.IsSuccessStatusCode) return Task.CompletedTask; throw Rejected(); }
    private static JudgeServerException Rejected() => new("worker.server_rejected", HttpStatusCode.BadRequest);
}
