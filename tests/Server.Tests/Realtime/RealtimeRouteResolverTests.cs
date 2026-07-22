using System.Text.Json;
using Mastemis.Application;
using Mastemis.Domain;
using Mastemis.Infrastructure.Persistence;
using Mastemis.Server.Realtime;
using Microsoft.EntityFrameworkCore;

namespace Mastemis.Server.Tests.Realtime;

public sealed class RealtimeRouteResolverTests
{
    [Fact]
    public async Task Warning_and_termination_route_to_candidate_room_exam_and_chief()
    {
        await using var db = CreateContext(); var session = SeedSession(db); await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var payload = JsonSerializer.Serialize(new WarningIssued(new(session.Id), WarningId.New(), 3));
        var routes = await new RealtimeRouteResolver(db).ResolveAsync(payload, TestContext.Current.CancellationToken);
        Assert.Equal(4, routes.Count); Assert.Contains($"room:{session.RoomId:D}", routes);
        Assert.Contains($"chief:{session.ExamId:D}", routes); Assert.Contains($"candidate:{session.CandidateId:D}", routes);
    }

    [Fact]
    public async Task Judgement_update_resolves_submission_to_candidate_session()
    {
        await using var db = CreateContext(); var session = SeedSession(db); var submissionId = Guid.NewGuid();
        db.Submissions.Add(new SubmissionRow
        {
            Id = submissionId,
            SessionId = session.Id,
            ProblemId = Guid.NewGuid(),
            RevisionId = Guid.NewGuid(),
            Language = "csharp",
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var payload = JsonSerializer.Serialize(new JudgementUpdated(new(submissionId), SubmissionState.Accepted));
        var routes = await new RealtimeRouteResolver(db).ResolveAsync(payload, TestContext.Current.CancellationToken);
        Assert.Contains($"candidate:{session.CandidateId:D}", routes);
    }

    [Fact]
    public async Task Worker_contract_routes_only_to_bound_worker_group()
    {
        await using var db = CreateContext(); var workerId = JudgeWorkerId.New();
        var payload = JsonSerializer.Serialize(new WorkerCapacityChanged(workerId, 4));
        Assert.Equal([$"worker:{workerId.Value:D}"], await new RealtimeRouteResolver(db).ResolveAsync(payload, TestContext.Current.CancellationToken));
    }

    private static SessionRow SeedSession(MastemisDbContext db)
    {
        var row = new SessionRow { Id = Guid.NewGuid(), ExamId = Guid.NewGuid(), RoomId = Guid.NewGuid(), CandidateId = Guid.NewGuid(), ConcurrencyToken = Guid.NewGuid() };
        db.ExamSessions.Add(row); return row;
    }
    private static MastemisDbContext CreateContext() => new(new DbContextOptionsBuilder<MastemisDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
