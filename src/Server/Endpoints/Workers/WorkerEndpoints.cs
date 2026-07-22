using System.Security.Claims;
using Mastemis.Application;
using Mastemis.Contracts.Judge;
using Mastemis.Domain;
using Mastemis.Infrastructure.Persistence;
using Mastemis.Infrastructure.Persistence.Queue;

namespace Mastemis.Server.Endpoints.Workers;

public static class WorkerEndpoints
{
    public static void MapWorkerEndpoints(this WebApplication app)
    {
        var administration = app.MapGroup("/api/admin").RequireAuthorization("Administrator");
        administration.MapPost("/workers", async (RegisterWorkerRequest request, IWorkerCredentialService credentials, CancellationToken ct) => Results.Created("/api/admin/workers", await credentials.RegisterAsync(request.Name, request.Capacity, request.ExpiresAtUtc, ct)));
        administration.MapPost("/workers/{workerId:guid}/rotate", async (Guid workerId, RotateWorkerRequest request, IWorkerCredentialService credentials, CancellationToken ct) => Results.Ok(await credentials.RotateAsync(new(workerId), request.ExpiresAtUtc, ct)));
        administration.MapDelete("/workers/{workerId:guid}/credential", async (Guid workerId, IWorkerCredentialService credentials, CancellationToken ct) => { await credentials.RevokeAsync(new(workerId), ct); return Results.NoContent(); });
        var workers = app.MapGroup("/api/worker").RequireAuthorization("WorkerOnly");
        workers.MapPost("/heartbeat", async (ClaimsPrincipal principal, WorkerHeartbeatContract request, WorkerCredentialService credentials, CancellationToken ct) => { await credentials.HeartbeatAsync(CurrentWorker(principal), request.Capacity, request.Languages, request.SandboxBackend, ct); return Results.NoContent(); });
        workers.MapPost("/jobs/claim", async (ClaimsPrincipal principal, ClaimJobRequest request, IWorkerJudgeQueue queue, CancellationToken ct) =>
        {
            var lease = await queue.ClaimAsync(CurrentWorker(principal), TimeSpan.FromSeconds(request.LeaseSeconds), ct);
            return lease is null ? Results.NoContent() : Results.Ok(new WorkerLeaseContract(lease.JobId, lease.SubmissionId,
                lease.LeaseId, lease.LeaseExpiresAtUtc, lease.Attempt, lease.MaximumAttempts));
        });
        workers.MapGet("/jobs/{jobId:guid}/contract", async (Guid jobId, Guid leaseId, ClaimsPrincipal principal, WorkerJobPayloadService payloads, CancellationToken ct) =>
            Results.Ok(await payloads.GetContractAsync(CurrentWorker(principal), new(jobId), leaseId, ct)));
        workers.MapGet("/jobs/{jobId:guid}/source", async (Guid jobId, Guid leaseId, ClaimsPrincipal principal, WorkerJobPayloadService payloads, CancellationToken ct) =>
            Results.Stream(await payloads.OpenSourceAsync(CurrentWorker(principal), new(jobId), leaseId, ct), "application/octet-stream"));
        workers.MapGet("/jobs/{jobId:guid}/tests/{index:int}/input", async (Guid jobId, int index, Guid leaseId, ClaimsPrincipal principal, WorkerJobPayloadService payloads, CancellationToken ct) =>
            Results.Stream(await payloads.OpenTestDataAsync(CurrentWorker(principal), new(jobId), leaseId, index, false, ct), "application/octet-stream"));
        workers.MapGet("/jobs/{jobId:guid}/tests/{index:int}/expected", async (Guid jobId, int index, Guid leaseId, ClaimsPrincipal principal, WorkerJobPayloadService payloads, CancellationToken ct) =>
            Results.Stream(await payloads.OpenTestDataAsync(CurrentWorker(principal), new(jobId), leaseId, index, true, ct), "application/octet-stream"));
        workers.MapPost("/jobs/{jobId:guid}/renew", async (Guid jobId, ClaimsPrincipal principal, LeaseRequest request, IWorkerJudgeQueue queue, CancellationToken ct) => { await queue.RenewAsync(CurrentWorker(principal), new(jobId), request.LeaseId, TimeSpan.FromSeconds(request.LeaseSeconds), ct); return Results.NoContent(); });
        workers.MapPost("/jobs/{jobId:guid}/start", async (Guid jobId, ClaimsPrincipal principal, LeaseRequest request, IWorkerJudgeQueue queue, CancellationToken ct) => { await queue.StartAsync(CurrentWorker(principal), new(jobId), request.LeaseId, ct); return Results.NoContent(); });
        workers.MapPost("/jobs/{jobId:guid}/complete", async (Guid jobId, ClaimsPrincipal principal, WorkerJudgementReport request, IWorkerJudgeQueue queue, IClock clock, CancellationToken ct) =>
        {
            var workerId = CurrentWorker(principal);
            if (request.Result.WorkerId != workerId || request.Result.Verdict is < SubmissionState.Accepted or > SubmissionState.InfrastructureError)
                throw new ApplicationFailure(ErrorCodes.Forbidden, "Worker identity or verdict is invalid.");
            var compilerSummary = string.Join(',', request.Result.CompilerDiagnostics.Take(100).Select(x => SafeCode(x.Code)));
            var completion = new WorkerJudgementCompletion(new(request.SubmissionId, request.Result.Verdict,
                request.Result.Verdict == SubmissionState.Accepted ? 100 : 0, clock.UtcNow), request.Result.FailedTestIndex,
                checked((long)request.Result.ExecutionTime.TotalMilliseconds), request.Result.PeakMemoryBytes,
                request.Result.ExitCode, request.Result.Signal, request.Result.StandardOutputBytes, request.Result.StandardErrorBytes,
                compilerSummary, SafeCode(request.Result.InfrastructureFailureReason), null, SafeText(request.Result.SandboxBackend, 100),
                workerId, SafeText(request.Result.JudgeVersion, 100));
            await queue.CompleteDetailedAsync(workerId, new(jobId), request.LeaseId, completion, ct);
            return Results.NoContent();
        });
        workers.MapPost("/jobs/{jobId:guid}/fail", async (Guid jobId, ClaimsPrincipal principal, WorkerFailureReport request, IWorkerJudgeQueue queue, CancellationToken ct) => { await queue.FailAsync(CurrentWorker(principal), new(jobId), request.LeaseId, SafeCode(request.FailureCode) ?? "worker.infrastructure", ct); return Results.NoContent(); });
    }
    private static JudgeWorkerId CurrentWorker(ClaimsPrincipal principal) => new(Guid.Parse(principal.FindFirst("worker_id")!.Value));
    private static string SafeText(string value, int maximumLength) => string.IsNullOrWhiteSpace(value) || value.Length > maximumLength
        ? throw new ApplicationFailure(ErrorCodes.InvalidInput, "Worker result metadata is invalid.") : value;
    private static string? SafeCode(string? value)
    {
        if (value is null) return null;
        if (value.Length is < 1 or > 100 || value.Any(x => !char.IsAsciiLetterOrDigit(x) && x is not '.' and not '_' and not '-'))
            throw new ApplicationFailure(ErrorCodes.InvalidInput, "Worker diagnostic code is invalid.");
        return value;
    }
}

public sealed record RegisterWorkerRequest(string Name, int Capacity, DateTimeOffset? ExpiresAtUtc);
public sealed record RotateWorkerRequest(DateTimeOffset? ExpiresAtUtc);
public sealed record ClaimJobRequest(int LeaseSeconds);
public sealed record LeaseRequest(Guid LeaseId, int LeaseSeconds);
