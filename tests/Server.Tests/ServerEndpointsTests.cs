using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Mastemis.Server.Tests;

public sealed class ServerEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    public ServerEndpointsTests(WebApplicationFactory<Program> factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Liveness_does_not_depend_on_postgresql()
    {
        var response = await _client.GetAsync("/health/live", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Readiness_reports_degraded_dependency_without_crashing()
    {
        var response = await _client.GetAsync("/health/ready", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Version_reports_no_project_telemetry()
    {
        var response = await _client.GetFromJsonAsync<VersionResponse>("/api/system/version", TestContext.Current.CancellationToken);
        Assert.NotNull(response);
        Assert.Equal("Mastemis", response.Product);
        Assert.Equal("none", response.Telemetry);
    }

    [Fact]
    public async Task Exam_can_be_created_and_opened()
    {
        var create = await _client.PostAsJsonAsync("/api/exams", new { title = "API exam", idempotencyKey = Guid.NewGuid().ToString() }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var exam = await create.Content.ReadFromJsonAsync<ExamDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(exam);
        var open = await _client.PostAsync($"/api/exams/{exam.Id}/open", null, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, open.StatusCode);
        Assert.True(open.Headers.Contains("X-Correlation-ID"));
    }

    private sealed record VersionResponse(string Product, string Version, string Telemetry);
    private sealed record ExamDto(Guid Id, string Title, string State);
}
