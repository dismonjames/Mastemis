using Mastemis.Application;
using Mastemis.Application.Evidence;
using Mastemis.Domain;
using Mastemis.Infrastructure.Persistence;
using Mastemis.Infrastructure.Persistence.Auditing;
using Microsoft.EntityFrameworkCore;

namespace Mastemis.Infrastructure.Tests.Evidence;

public sealed class EvidenceMetadataAccessTests
{
    [Fact]
    public async Task Reviewer_requires_explicit_grant_and_each_successful_access_is_audited()
    {
        await using var db = CreateContext(); var package = SeedPackage(db); var reviewer = new TestActor(MastemisRoles.EvidenceReviewer);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = new EvidenceMetadataAccess(db, reviewer, new FixedClock());

        await Assert.ThrowsAsync<ApplicationFailure>(() => service.GetAsync(new(package.Id), TestContext.Current.CancellationToken));
        var administrator = new EvidenceMetadataAccess(db, new TestActor(MastemisRoles.Administrator), new FixedClock());
        await administrator.GrantAsync(new(package.Id), reviewer.UserId, TestContext.Current.CancellationToken);
        await service.GetAsync(new(package.Id), TestContext.Current.CancellationToken);
        await service.GetAsync(new(package.Id), TestContext.Current.CancellationToken);

        Assert.Equal(2, await db.AuditRecords.CountAsync(x => x.Action == "evidence.metadata.read", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Administrator_without_grant_and_candidate_are_denied()
    {
        await using var db = CreateContext(); var package = SeedPackage(db); await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        foreach (var role in new[] { MastemisRoles.Administrator, MastemisRoles.Candidate })
        {
            var service = new EvidenceMetadataAccess(db, new TestActor(role), new FixedClock());
            await Assert.ThrowsAsync<ApplicationFailure>(() => service.GetAsync(new(package.Id), TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task Room_and_chief_invigilators_are_limited_to_assigned_scope()
    {
        await using var db = CreateContext(); var package = SeedPackage(db); var roomActor = new TestActor(MastemisRoles.RoomInvigilator);
        var chiefActor = new TestActor(MastemisRoles.ChiefInvigilator);
        db.RoomAssignments.Add(new RoomAssignmentRow { RoomId = package.RoomId, UserId = roomActor.UserId.Value });
        db.ExamAssignments.Add(new ExamAssignmentRow { ExamId = package.ExamId, UserId = chiefActor.UserId.Value, Role = MastemisRoles.ChiefInvigilator });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        _ = await new EvidenceMetadataAccess(db, roomActor, new FixedClock()).GetAsync(new(package.Id), TestContext.Current.CancellationToken);
        _ = await new EvidenceMetadataAccess(db, chiefActor, new FixedClock()).GetAsync(new(package.Id), TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<ApplicationFailure>(() => new EvidenceMetadataAccess(db, new TestActor(MastemisRoles.RoomInvigilator), new FixedClock()).GetAsync(new(package.Id), TestContext.Current.CancellationToken));
    }

    private static EvidencePackageRow SeedPackage(MastemisDbContext db)
    {
        var row = new EvidencePackageRow
        {
            Id = Guid.NewGuid(),
            ExamId = Guid.NewGuid(),
            RoomId = Guid.NewGuid(),
            CandidateId = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            CreatedAtUtc = new FixedClock().UtcNow
        };
        db.EvidencePackages.Add(row); return row;
    }
    private static MastemisDbContext CreateContext() => new(new DbContextOptionsBuilder<MastemisDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private sealed class FixedClock : IClock { public DateTimeOffset UtcNow { get; } = new(2026, 7, 22, 5, 0, 0, TimeSpan.Zero); }
    private sealed class TestActor(params string[] roles) : IEvidenceActor
    {
        public UserId UserId { get; } = UserId.New();
        public bool IsInRole(string role) => roles.Contains(role, StringComparer.Ordinal);
    }
}
