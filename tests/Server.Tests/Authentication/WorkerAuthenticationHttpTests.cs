using System.Net;
using System.Security.Claims;
using Mastemis.Application;
using Mastemis.Domain;
using Mastemis.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Mastemis.Server.Tests.Authentication;

public sealed class WorkerAuthenticationHttpTests
{
    [Fact]
    public async Task Valid_credential_is_bound_to_its_worker_identifier()
    {
        var worker = Guid.NewGuid(); await using var host = await CreateHostAsync(new FakeCredentials(worker, "correct"));
        using var client = host.GetTestClient(); client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Worker {worker:D}.correct");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/worker/{worker:D}", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/worker/{Guid.NewGuid():D}", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Bearer token")]
    [InlineData("Worker malformed")]
    [InlineData("Worker 00000000-0000-0000-0000-000000000000.incorrect")]
    public async Task Invalid_unknown_and_malformed_credentials_are_rejected(string authorization)
    {
        await using var host = await CreateHostAsync(new FakeCredentials(Guid.NewGuid(), "correct"));
        using var client = host.GetTestClient(); if (authorization.Length > 0) client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authorization);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/worker/{Guid.NewGuid():D}", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Worker_identity_cannot_use_human_administration_endpoint()
    {
        var worker = Guid.NewGuid(); await using var host = await CreateHostAsync(new FakeCredentials(worker, "correct"));
        using var client = host.GetTestClient(); client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Worker {worker:D}.correct");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/admin", TestContext.Current.CancellationToken)).StatusCode);
    }

    private static async Task<WebApplication> CreateHostAsync(IWorkerCredentialService credentials)
    {
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(credentials);
        builder.Services.AddAuthentication(WorkerAuthenticationDefaults.Scheme)
            .AddScheme<AuthenticationSchemeOptions, WorkerAuthenticationHandler>(WorkerAuthenticationDefaults.Scheme, _ => { });
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy("WorkerOnly", policy => policy.AddAuthenticationSchemes(WorkerAuthenticationDefaults.Scheme).RequireRole(MastemisRoles.JudgeWorker))
            .AddPolicy("Administrator", policy => policy.RequireRole(MastemisRoles.Administrator));
        var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization();
        app.MapGet("/worker/{workerId:guid}", (Guid workerId, ClaimsPrincipal principal) =>
            principal.FindFirstValue("worker_id") == workerId.ToString("D") ? Results.Ok() : Results.Forbid()).RequireAuthorization("WorkerOnly");
        app.MapGet("/admin", () => Results.Ok()).RequireAuthorization("Administrator");
        await app.StartAsync(TestContext.Current.CancellationToken); return app;
    }

    private sealed class FakeCredentials(Guid workerId, string secret) : IWorkerCredentialService
    {
        public Task<bool> AuthenticateAsync(JudgeWorkerId id, string value, CancellationToken cancellationToken)
        { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(id.Value == workerId && value == secret); }
        public Task<IssuedWorkerCredential> RegisterAsync(string name, int capacity, DateTimeOffset? expiresAtUtc, CancellationToken cancellationToken) => throw new InvalidOperationException();
        public Task<IssuedWorkerCredential> RotateAsync(JudgeWorkerId id, DateTimeOffset? expiresAtUtc, CancellationToken cancellationToken) => throw new InvalidOperationException();
        public Task RevokeAsync(JudgeWorkerId id, CancellationToken cancellationToken) => throw new InvalidOperationException();
        public Task HeartbeatAsync(JudgeWorkerId id, int capacity, CancellationToken cancellationToken) => throw new InvalidOperationException();
    }
}
