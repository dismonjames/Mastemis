using Mastemis.Domain;
using Mastemis.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Mastemis.Infrastructure.Tests;

public sealed class WorkerCredentialTests
{
    [Fact]
    public async Task Secret_is_verified_and_only_hash_is_persisted()
    {
        await using var db = CreateContext(); var clock = new FixedClock();
        var service = new WorkerCredentialService(db, clock, new PasswordHasher<WorkerCredentialRow>());
        var issued = await service.RegisterAsync("worker", 2, null, TestContext.Current.CancellationToken);
        Assert.True(await service.AuthenticateAsync(issued.WorkerId, issued.Secret, TestContext.Current.CancellationToken));
        Assert.DoesNotContain(issued.Secret, (await db.WorkerCredentials.SingleAsync(TestContext.Current.CancellationToken)).SecretHash, StringComparison.Ordinal);
        Assert.False(await service.AuthenticateAsync(issued.WorkerId, "incorrect", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Rotation_revokes_old_secret_and_accepts_new_secret()
    {
        await using var db = CreateContext(); var service = new WorkerCredentialService(db, new FixedClock(), new PasswordHasher<WorkerCredentialRow>());
        var first = await service.RegisterAsync("worker", 2, null, TestContext.Current.CancellationToken);
        var second = await service.RotateAsync(first.WorkerId, null, TestContext.Current.CancellationToken);
        Assert.False(await service.AuthenticateAsync(first.WorkerId, first.Secret, TestContext.Current.CancellationToken));
        Assert.True(await service.AuthenticateAsync(second.WorkerId, second.Secret, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Revoked_and_expired_credentials_are_rejected()
    {
        await using var db = CreateContext(); var clock = new FixedClock();
        var service = new WorkerCredentialService(db, clock, new PasswordHasher<WorkerCredentialRow>());
        var expired = await service.RegisterAsync("expired", 1, clock.UtcNow.AddSeconds(-1), TestContext.Current.CancellationToken);
        Assert.False(await service.AuthenticateAsync(expired.WorkerId, expired.Secret, TestContext.Current.CancellationToken));
        var active = await service.RegisterAsync("active", 1, null, TestContext.Current.CancellationToken);
        await service.RevokeAsync(active.WorkerId, TestContext.Current.CancellationToken);
        Assert.False(await service.AuthenticateAsync(active.WorkerId, active.Secret, TestContext.Current.CancellationToken));
    }

    private static MastemisDbContext CreateContext() => new(new DbContextOptionsBuilder<MastemisDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private sealed class FixedClock : Mastemis.Application.IClock { public DateTimeOffset UtcNow { get; } = new(2026, 7, 22, 4, 0, 0, TimeSpan.Zero); }
}
