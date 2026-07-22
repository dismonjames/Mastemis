using System.Security.Claims;
using Mastemis.Application;
using Mastemis.Domain;
using Mastemis.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Mastemis.Server.Tests;

public sealed class AuthorizationScopeTests
{
    [Fact]
    public async Task Room_invigilator_cannot_substitute_another_room()
    {
        await using var db = CreateContext(); var userId = Guid.NewGuid(); var ownRoom = Guid.NewGuid(); var otherRoom = Guid.NewGuid();
        db.RoomAssignments.Add(new RoomAssignmentRow { RoomId = ownRoom, UserId = userId, AssignedAtUtc = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var authorization = CreateAuthorization(db, userId, MastemisRoles.RoomInvigilator);
        await authorization.EnsureAsync("room.realtime", ownRoom, TestContext.Current.CancellationToken);
        var failure = await Assert.ThrowsAsync<ApplicationFailure>(async () => await authorization.EnsureAsync("room.realtime", otherRoom, TestContext.Current.CancellationToken));
        Assert.Equal(ErrorCodes.Forbidden, failure.Code);
    }

    [Fact]
    public async Task Candidate_cannot_access_another_candidates_session_or_submission()
    {
        await using var db = CreateContext(); var user = Guid.NewGuid(); var otherUser = Guid.NewGuid();
        var candidate = Guid.NewGuid(); var otherCandidate = Guid.NewGuid(); var exam = Guid.NewGuid(); var session = Guid.NewGuid(); var submission = Guid.NewGuid();
        db.Candidates.AddRange(new CandidateRow { Id = candidate, UserId = user }, new CandidateRow { Id = otherCandidate, UserId = otherUser });
        db.CandidateRegistrations.Add(new CandidateRegistrationRow { Id = Guid.NewGuid(), ExamId = exam, CandidateId = otherCandidate, RegistrationCode = "O", AccessState = (int)CandidateExamAccessState.Enabled });
        db.ExamSessions.Add(new SessionRow { Id = session, ExamId = exam, RoomId = Guid.NewGuid(), CandidateId = otherCandidate, State = (int)SessionState.Active, ConcurrencyToken = Guid.NewGuid() });
        db.Submissions.Add(new SubmissionRow { Id = submission, SessionId = session, ProblemId = Guid.NewGuid(), RevisionId = Guid.NewGuid(), Language = "csharp", CreatedAtUtc = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var authorization = CreateAuthorization(db, user, MastemisRoles.Candidate);
        await Assert.ThrowsAsync<ApplicationFailure>(async () => await authorization.EnsureAsync("session.access", session, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ApplicationFailure>(async () => await authorization.EnsureAsync("submission.read", submission, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Disabled_candidate_account_cannot_write_own_session()
    {
        await using var db = CreateContext(); var user = Guid.NewGuid(); var candidate = Guid.NewGuid(); var exam = Guid.NewGuid(); var session = Guid.NewGuid();
        db.Candidates.Add(new CandidateRow { Id = candidate, UserId = user });
        db.CandidateRegistrations.Add(new CandidateRegistrationRow { Id = Guid.NewGuid(), ExamId = exam, CandidateId = candidate, RegistrationCode = "C", AccessState = (int)CandidateExamAccessState.Terminated });
        db.ExamSessions.Add(new SessionRow { Id = session, ExamId = exam, RoomId = Guid.NewGuid(), CandidateId = candidate, State = (int)SessionState.Terminated, ConcurrencyToken = Guid.NewGuid() });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var authorization = CreateAuthorization(db, user, MastemisRoles.Candidate);
        await Assert.ThrowsAsync<ApplicationFailure>(async () => await authorization.EnsureAsync("session.write", session, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ApplicationFailure>(async () => await authorization.EnsureAsync("candidate.realtime", candidate, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Chief_realtime_access_requires_an_explicit_assignment()
    {
        await using var db = CreateContext(); var user = Guid.NewGuid(); var exam = Guid.NewGuid();
        var authorization = CreateAuthorization(db, user, MastemisRoles.ChiefInvigilator);
        await Assert.ThrowsAsync<ApplicationFailure>(async () => await authorization.EnsureAsync("chief.realtime", exam, TestContext.Current.CancellationToken));
        db.ExamAssignments.Add(new ExamAssignmentRow { ExamId = exam, UserId = user, Role = MastemisRoles.ChiefInvigilator });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await authorization.EnsureAsync("chief.realtime", exam, TestContext.Current.CancellationToken);
    }

    private static ProductionApplicationAuthorization CreateAuthorization(MastemisDbContext db, Guid userId, string role)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, userId.ToString("D")), new Claim(ClaimTypes.Role, role)], "test"))
        };
        return new ProductionApplicationAuthorization(new HttpContextAccessor { HttpContext = context }, db, new Mastemis.Infrastructure.SystemClock());
    }
    private static MastemisDbContext CreateContext() => new(new DbContextOptionsBuilder<MastemisDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
