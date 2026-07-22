using Mastemis.Application;
using Mastemis.Application.Administration;
using Mastemis.Domain;
using Mastemis.Infrastructure.Persistence;
using Mastemis.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mastemis.Infrastructure.Tests.Identity;

public sealed class AdministrationTests
{
    [Fact]
    public async Task Administrator_creates_user_and_duplicate_normalized_name_is_rejected()
    {
        await using var provider = CreateIdentityProvider();
        await SeedRolesAsync(provider);
        var service = new HumanIdentityAdministration(provider.GetRequiredService<UserManager<ApplicationUser>>(), new FixedClock());

        var created = await service.CreateAsync(" Invigilator ", "Room Invigilator", "Strong!Pass123", TestContext.Current.CancellationToken);

        Assert.Equal("Invigilator", created.Username);
        var failure = await Assert.ThrowsAsync<ApplicationFailure>(() =>
            service.CreateAsync("INVIGILATOR", "Duplicate", "Strong!Pass123", TestContext.Current.CancellationToken));
        Assert.Equal("identity.user_conflict", failure.Code);
    }

    [Fact]
    public async Task Human_roles_are_idempotent_and_worker_role_is_rejected()
    {
        await using var provider = CreateIdentityProvider();
        await SeedRolesAsync(provider);
        var service = new HumanIdentityAdministration(provider.GetRequiredService<UserManager<ApplicationUser>>(), new FixedClock());
        var created = await service.CreateAsync("manager", "Manager", "Strong!Pass123", TestContext.Current.CancellationToken);

        await service.AssignRoleAsync(created.UserId, MastemisRoles.ExamManager, TestContext.Current.CancellationToken);
        await service.AssignRoleAsync(created.UserId, MastemisRoles.ExamManager, TestContext.Current.CancellationToken);
        Assert.Equal([MastemisRoles.ExamManager], (await service.GetStatusAsync(created.UserId, TestContext.Current.CancellationToken)).Roles);
        await Assert.ThrowsAsync<ApplicationFailure>(() => service.AssignRoleAsync(created.UserId, MastemisRoles.JudgeWorker, TestContext.Current.CancellationToken));
        await service.RemoveRoleAsync(created.UserId, MastemisRoles.ExamManager, TestContext.Current.CancellationToken);
        Assert.Empty((await service.GetStatusAsync(created.UserId, TestContext.Current.CancellationToken)).Roles);
    }

    [Fact]
    public async Task Scope_assignments_are_idempotent_and_cross_exam_self_escalation_is_rejected()
    {
        await using var db = CreateContext();
        var examId = Guid.NewGuid(); var otherExamId = Guid.NewGuid(); var roomId = Guid.NewGuid(); var userId = new UserId(Guid.NewGuid());
        db.Exams.AddRange(new ExamRow { Id = examId }, new ExamRow { Id = otherExamId });
        db.Rooms.Add(new RoomRow { Id = roomId, ExamId = examId, Code = "A", Name = "A" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var actor = new TestActor(new(Guid.NewGuid()), MastemisRoles.Administrator);
        var service = new ScopeAdministration(db, actor, new FixedClock());

        await service.AssignExamAsync(new(examId), userId, MastemisRoles.ChiefInvigilator, TestContext.Current.CancellationToken);
        await service.AssignExamAsync(new(examId), userId, MastemisRoles.ChiefInvigilator, TestContext.Current.CancellationToken);
        await service.AssignRoomAsync(new(roomId), userId, TestContext.Current.CancellationToken);

        Assert.Single(await service.ListExamAsync(new(examId), TestContext.Current.CancellationToken));
        Assert.Single(await service.ListRoomAsync(new(roomId), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ApplicationFailure>(() => service.AssignExamAsync(new(otherExamId), actor.UserId, MastemisRoles.ExamManager, TestContext.Current.CancellationToken));
    }

    private static ServiceProvider CreateIdentityProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<MastemisDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddIdentityCore<ApplicationUser>(options => options.User.RequireUniqueEmail = false)
            .AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<MastemisDbContext>();
        return services.BuildServiceProvider();
    }

    private static async Task SeedRolesAsync(IServiceProvider provider)
    {
        var roles = provider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in MastemisRoles.All) await roles.CreateAsync(new IdentityRole<Guid>(role));
    }

    private static MastemisDbContext CreateContext() => new(new DbContextOptionsBuilder<MastemisDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private sealed class FixedClock : IClock { public DateTimeOffset UtcNow { get; } = new(2026, 7, 22, 4, 0, 0, TimeSpan.Zero); }
    private sealed class TestActor(UserId userId, params string[] roles) : IAdministrationActor
    {
        public UserId UserId { get; } = userId;
        public bool IsInRole(string role) => roles.Contains(role, StringComparer.Ordinal);
    }
}
