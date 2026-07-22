using System.Net;
using System.Text;
using Mastemis.Domain;
using Mastemis.Judge.Configuration;
using Mastemis.Judge.Worker;

namespace Mastemis.Judge.Tests.Worker;

public sealed class JudgeServerClientTests
{
    [Fact]
    public async Task Binds_worker_identity_and_secret_to_authorization_header()
    {
        var workerId = JudgeWorkerId.New();
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.NoContent));
        var client = CreateClient(handler, workerId, "one-time-secret");

        var lease = await client.ClaimAsync(60, TestContext.Current.CancellationToken);

        Assert.Null(lease);
        Assert.Equal($"Worker {workerId.Value:D}.one-time-secret", handler.Authorization);
        Assert.Equal("/api/worker/jobs/claim", handler.RequestUri?.AbsolutePath);
    }

    [Fact]
    public async Task Rejects_download_that_exceeds_declared_bound()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(Encoding.UTF8.GetBytes("too-large"))
        };
        var client = CreateClient(new RecordingHandler(response), JudgeWorkerId.New(), "secret");

        var error = await Assert.ThrowsAsync<JudgeServerException>(() => client.GetSourceAsync(
            JudgeJobId.New(), Guid.NewGuid(), 3, TestContext.Current.CancellationToken));

        Assert.Equal("worker.download_too_large", error.Code);
    }

    private static JudgeServerClient CreateClient(HttpMessageHandler handler, JudgeWorkerId workerId, string secret)
    {
        var options = new JudgeWorkerOptions(new("https://mastemis.example/"), workerId, secret, 1,
            TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(20),
            TimeSpan.FromSeconds(5), Path.GetFullPath("workspaces"));
        return new(new HttpClient(handler), options);
    }

    private sealed class RecordingHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public string? Authorization { get; private set; }
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization?.ToString();
            RequestUri = request.RequestUri;
            return Task.FromResult(response);
        }
    }
}
