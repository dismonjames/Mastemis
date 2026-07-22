using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Mastemis.Infrastructure.Persistence;

public static class MastemisRoles
{
    public const string Administrator = "Administrator";
    public const string ExamManager = "ExamManager";
    public const string ChiefInvigilator = "ChiefInvigilator";
    public const string RoomInvigilator = "RoomInvigilator";
    public const string Candidate = "Candidate";
    public const string JudgeWorker = "JudgeWorker";
    public const string EvidenceReviewer = "EvidenceReviewer";
    public static IReadOnlyList<string> All => [Administrator, ExamManager, ChiefInvigilator, RoomInvigilator, Candidate, JudgeWorker, EvidenceReviewer];
}

public sealed class IdentityBootstrapService(IServiceScopeFactory scopeFactory, IConfiguration configuration, Mastemis.Application.IClock clock,
    ILogger<IdentityBootstrapService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MastemisDbContext>();
        if (!string.Equals(configuration["Database:ApplyMigrations"], "false", StringComparison.OrdinalIgnoreCase))
            await db.Database.MigrateAsync(cancellationToken);
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in MastemisRoles.All)
            if (!await roles.RoleExistsAsync(role))
            {
                var result = await roles.CreateAsync(new IdentityRole<Guid>(role));
                if (!result.Succeeded) throw new InvalidOperationException("Unable to initialize required roles.");
            }
        var username = configuration["Bootstrap:Administrator:Username"];
        var password = configuration["Bootstrap:Administrator:Password"];
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password)) return;
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        if (await users.FindByNameAsync(username) is not null) return;
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = username,
            DisplayName = username,
            EmailConfirmed = true,
            CreatedAtUtc = clock.UtcNow
        };
        var created = await users.CreateAsync(user, password);
        if (!created.Succeeded) throw new InvalidOperationException("Administrator bootstrap configuration did not satisfy identity policy.");
        var assigned = await users.AddToRoleAsync(user, MastemisRoles.Administrator);
        if (!assigned.Succeeded) throw new InvalidOperationException("Unable to assign the administrator role.");
        logger.LogInformation("Initial administrator account created from secure configuration.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
