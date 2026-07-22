using Mastemis.Application;
using Mastemis.Infrastructure.Persistence;
using Mastemis.Infrastructure.Persistence.Outbox;
using Mastemis.Infrastructure.Storage.ProblemObjects;
using Mastemis.Infrastructure.Storage.Reconciliation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

public sealed class PostgresHealthCheck(IConfiguration configuration) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var connectionString = configuration.GetConnectionString("Mastemis");
        if (string.IsNullOrWhiteSpace(connectionString))
            return HealthCheckResult.Degraded("PostgreSQL is not configured; volatile development storage is active.");
        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand("SELECT 1", connection);
            _ = await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy("PostgreSQL is reachable.");
        }
        catch (Exception exception) when (exception is NpgsqlException or TimeoutException)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL is unavailable.");
        }
    }
}

public sealed class DatabaseSchemaHealthCheck(IServiceScopeFactory scopeFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MastemisDbContext>();
        var pending = await db.Database.GetPendingMigrationsAsync(cancellationToken);
        return pending.Any() ? HealthCheckResult.Unhealthy("Database migrations are pending.") : HealthCheckResult.Healthy("Identity and application schema are current.");
    }
}

public sealed class OutboxHealthCheck(OutboxStatus status) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(status.LastSuccessUtc is null
            ? HealthCheckResult.Degraded("Outbox dispatcher has not published a message in this process.")
            : HealthCheckResult.Healthy("Outbox dispatcher has published successfully."));
    }
}

public sealed class JudgeQueueHealthCheck(IServiceScopeFactory scopeFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MastemisDbContext>();
        _ = await db.JudgeJobs.AsNoTracking().CountAsync(cancellationToken);
        return HealthCheckResult.Healthy("Durable judge queue is queryable.");
    }
}

public sealed class StorageHealthCheck(IConfiguration configuration) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var root = Path.GetFullPath(configuration["Storage:Path"] ?? Path.Combine(AppContext.BaseDirectory, "storage"));
        try
        {
            Directory.CreateDirectory(root);
            return Task.FromResult(HealthCheckResult.Healthy("Storage path is available."));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Storage path is unavailable."));
        }
    }
}

public sealed class SourceReconciliationHealthCheck(SourceReconciliationStatus status) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(status.Failed ? HealthCheckResult.Degraded("The latest source reconciliation pass failed; cleanup is suspended until database verification succeeds.")
            : HealthCheckResult.Healthy(status.LastSuccessUtc is null ? "Source reconciliation is configured and awaiting its first pass." : "Source reconciliation completed successfully."));
    }
}

public sealed class ProblemObjectReconciliationHealthCheck(ProblemObjectReconciliationStatus status) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(status.Failed
            ? HealthCheckResult.Degraded("Problem object reconciliation failed; destructive cleanup is suspended.")
            : HealthCheckResult.Healthy(status.LastSuccessUtc is null
                ? "Problem object reconciliation is awaiting its first pass."
                : "Problem object reconciliation completed successfully."));
}

public sealed class ReferenceOutputQueueHealthCheck(IServiceScopeFactory scopes) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MastemisDbContext>();
        _ = await db.ReferenceOutputJobs.AsNoTracking().CountAsync(cancellationToken);
        return HealthCheckResult.Healthy("Reference output queue is queryable.");
    }
}

[Authorize(AuthenticationSchemes = "Identity.Application,WorkerSecret")]
public sealed class ExamHub : Hub
{
    public async Task JoinExam(string examId)
    {
        if (!Guid.TryParse(examId, out var parsed)) throw new HubException(ErrorCodes.InvalidInput);
        var authorization = Context.GetHttpContext()!.RequestServices.GetRequiredService<Mastemis.Application.IAuthorizationService>();
        await authorization.EnsureAsync("exam.realtime", parsed, Context.ConnectionAborted);
        await Groups.AddToGroupAsync(Context.ConnectionId, $"exam:{parsed:D}", Context.ConnectionAborted);
    }

    public Task JoinRoom(string roomId) => JoinAuthorizedAsync("room.realtime", "room", roomId);
    public Task JoinCandidate(string candidateId) => JoinAuthorizedAsync("candidate.realtime", "candidate", candidateId);
    public Task JoinProblem(string problemId) => JoinAuthorizedAsync("problem.read", "problem", problemId);
    public async Task JoinChief(string examId)
    {
        if (!Guid.TryParse(examId, out var parsed)) throw new HubException(ErrorCodes.InvalidInput);
        var authorization = Context.GetHttpContext()!.RequestServices.GetRequiredService<Mastemis.Application.IAuthorizationService>();
        await authorization.EnsureAsync("chief.realtime", parsed, Context.ConnectionAborted);
        await Groups.AddToGroupAsync(Context.ConnectionId, $"chief:{parsed:D}", Context.ConnectionAborted);
    }

    public async Task JoinWorker(string workerId)
    {
        if (!Guid.TryParse(workerId, out var parsed) || Context.User?.FindFirst("worker_id")?.Value != parsed.ToString("D"))
            throw new HubException(ErrorCodes.Forbidden);
        await Groups.AddToGroupAsync(Context.ConnectionId, $"worker:{parsed:D}", Context.ConnectionAborted);
    }

    private async Task JoinAuthorizedAsync(string permission, string prefix, string value)
    {
        if (!Guid.TryParse(value, out var parsed)) throw new HubException(ErrorCodes.InvalidInput);
        var authorization = Context.GetHttpContext()!.RequestServices.GetRequiredService<Mastemis.Application.IAuthorizationService>();
        await authorization.EnsureAsync(permission, parsed, Context.ConnectionAborted);
        await Groups.AddToGroupAsync(Context.ConnectionId, $"{prefix}:{parsed:D}", Context.ConnectionAborted);
    }
}
