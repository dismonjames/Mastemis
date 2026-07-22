using System.Net;
using System.Net.Http.Json;
using Mastemis.Contracts.Judge;
using Mastemis.Domain;
using Mastemis.Judge.Configuration;

namespace Mastemis.Judge.Worker;

public sealed class JudgeServerClient(HttpClient client, JudgeWorkerOptions options) : IJudgeServerClient
{
    public async Task HeartbeatAsync(WorkerHeartbeatContract heartbeat, CancellationToken cancellationToken) =>
        await SendAsync(HttpMethod.Post, "api/worker/heartbeat", heartbeat, cancellationToken);
    public async Task<WorkerLeaseContract?> ClaimAsync(int leaseSeconds, CancellationToken cancellationToken)
    {
        using var response = await SendRawAsync(HttpMethod.Post, "api/worker/jobs/claim", new { leaseSeconds }, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NoContent) return null;
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<WorkerLeaseContract>(cancellationToken: cancellationToken);
    }
    public Task<WorkerJudgeContract> GetContractAsync(JudgeJobId jobId, Guid leaseId, CancellationToken cancellationToken) =>
        GetJsonAsync<WorkerJudgeContract>($"api/worker/jobs/{jobId.Value:D}/contract?leaseId={leaseId:D}", cancellationToken);
    public Task<byte[]> GetSourceAsync(JudgeJobId jobId, Guid leaseId, long maximumBytes, CancellationToken cancellationToken) =>
        GetBytesAsync($"api/worker/jobs/{jobId.Value:D}/source?leaseId={leaseId:D}", maximumBytes, cancellationToken);
    public Task<byte[]> GetTestInputAsync(JudgeJobId jobId, Guid leaseId, int index, long maximumBytes, CancellationToken cancellationToken) =>
        GetBytesAsync($"api/worker/jobs/{jobId.Value:D}/tests/{index}/input?leaseId={leaseId:D}", maximumBytes, cancellationToken);
    public Task<byte[]> GetExpectedOutputAsync(JudgeJobId jobId, Guid leaseId, int index, long maximumBytes, CancellationToken cancellationToken) =>
        GetBytesAsync($"api/worker/jobs/{jobId.Value:D}/tests/{index}/expected?leaseId={leaseId:D}", maximumBytes, cancellationToken);
    public async Task StartAsync(JudgeJobId jobId, Guid leaseId, CancellationToken cancellationToken) =>
        await SendAsync(HttpMethod.Post, $"api/worker/jobs/{jobId.Value:D}/start", new { leaseId }, cancellationToken);
    public async Task RenewAsync(JudgeJobId jobId, WorkerLeaseRenewal renewal, CancellationToken cancellationToken) =>
        await SendAsync(HttpMethod.Post, $"api/worker/jobs/{jobId.Value:D}/renew", renewal, cancellationToken);
    public async Task CompleteAsync(JudgeJobId jobId, WorkerJudgementReport report, CancellationToken cancellationToken) =>
        await SendAsync(HttpMethod.Post, $"api/worker/jobs/{jobId.Value:D}/complete", report, cancellationToken);
    public async Task FailAsync(JudgeJobId jobId, WorkerFailureReport report, CancellationToken cancellationToken) =>
        await SendAsync(HttpMethod.Post, $"api/worker/jobs/{jobId.Value:D}/fail", report, cancellationToken);

    private async Task<T> GetJsonAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, path); using var response = await client.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken)
            ?? throw new JudgeServerException("worker.invalid_response", response.StatusCode);
    }
    private async Task<byte[]> GetBytesAsync(string path, long maximumBytes, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, path); using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        if (response.Content.Headers.ContentLength > maximumBytes) throw new JudgeServerException("worker.download_too_large", response.StatusCode);
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken); using var output = new MemoryStream();
        var buffer = new byte[81920]; long total = 0;
        while (true)
        {
            var count = await input.ReadAsync(buffer, cancellationToken); if (count == 0) return output.ToArray();
            total += count; if (total > maximumBytes) throw new JudgeServerException("worker.download_too_large", response.StatusCode);
            await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
        }
    }
    private async Task SendAsync<T>(HttpMethod method, string path, T value, CancellationToken cancellationToken)
    { using var response = await SendRawAsync(method, path, value, cancellationToken); await EnsureSuccessAsync(response, cancellationToken); }
    private async Task<HttpResponseMessage> SendRawAsync<T>(HttpMethod method, string path, T value, CancellationToken cancellationToken)
    { using var request = CreateRequest(method, path); request.Content = JsonContent.Create(value); return await client.SendAsync(request, cancellationToken); }
    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, new Uri(options.ServerUrl, path));
        request.Headers.TryAddWithoutValidation("Authorization", $"Worker {options.WorkerId.Value:D}.{options.Secret}"); return request;
    }
    private static Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (response.IsSuccessStatusCode) return Task.CompletedTask;
        throw new JudgeServerException("worker.server_rejected", response.StatusCode);
    }
}

public sealed class JudgeServerException(string code, HttpStatusCode statusCode) : Exception("The judge server rejected the operation.")
{ public string Code { get; } = code; public HttpStatusCode StatusCode { get; } = statusCode; }
