using System.Security.Claims;
using Mastemis.Application;
using Mastemis.Domain;

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
        workers.MapPost("/heartbeat", async (ClaimsPrincipal principal, WorkerHeartbeatRequest request, IWorkerCredentialService credentials, CancellationToken ct) => { await credentials.HeartbeatAsync(CurrentWorker(principal), request.Capacity, ct); return Results.NoContent(); });
        workers.MapPost("/jobs/claim", async (ClaimsPrincipal principal, ClaimJobRequest request, IWorkerJudgeQueue queue, CancellationToken ct) => await queue.ClaimAsync(CurrentWorker(principal), TimeSpan.FromSeconds(request.LeaseSeconds), ct));
        workers.MapPost("/jobs/{jobId:guid}/renew", async (Guid jobId, ClaimsPrincipal principal, LeaseRequest request, IWorkerJudgeQueue queue, CancellationToken ct) => { await queue.RenewAsync(CurrentWorker(principal), new(jobId), request.LeaseId, TimeSpan.FromSeconds(request.LeaseSeconds), ct); return Results.NoContent(); });
        workers.MapPost("/jobs/{jobId:guid}/start", async (Guid jobId, ClaimsPrincipal principal, LeaseRequest request, IWorkerJudgeQueue queue, CancellationToken ct) => { await queue.StartAsync(CurrentWorker(principal), new(jobId), request.LeaseId, ct); return Results.NoContent(); });
        workers.MapPost("/jobs/{jobId:guid}/complete", async (Guid jobId, ClaimsPrincipal principal, CompleteJobRequest request, IWorkerJudgeQueue queue, IClock clock, CancellationToken ct) => { await queue.CompleteAsync(CurrentWorker(principal), new(jobId), request.LeaseId, new(new(request.SubmissionId), request.Verdict, request.Score, clock.UtcNow), ct); return Results.NoContent(); });
        workers.MapPost("/jobs/{jobId:guid}/fail", async (Guid jobId, ClaimsPrincipal principal, FailJobRequest request, IWorkerJudgeQueue queue, CancellationToken ct) => { await queue.FailAsync(CurrentWorker(principal), new(jobId), request.LeaseId, request.FailureCode, ct); return Results.NoContent(); });
    }
    private static JudgeWorkerId CurrentWorker(ClaimsPrincipal principal) => new(Guid.Parse(principal.FindFirst("worker_id")!.Value));
}

public sealed record RegisterWorkerRequest(string Name, int Capacity, DateTimeOffset? ExpiresAtUtc);
public sealed record RotateWorkerRequest(DateTimeOffset? ExpiresAtUtc);
public sealed record WorkerHeartbeatRequest(int Capacity);
public sealed record ClaimJobRequest(int LeaseSeconds);
public sealed record LeaseRequest(Guid LeaseId, int LeaseSeconds);
public sealed record CompleteJobRequest(Guid LeaseId, Guid SubmissionId, SubmissionState Verdict, int Score);
public sealed record FailJobRequest(Guid LeaseId, string FailureCode);
