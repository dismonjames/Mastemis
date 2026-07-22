using Mastemis.Application;
using Mastemis.Application.Administration;
using Mastemis.Domain;
using Microsoft.AspNetCore.Identity;

namespace Mastemis.Infrastructure.Persistence.Identity;

public sealed class HumanIdentityAdministration(UserManager<ApplicationUser> users, IClock clock)
    : IHumanIdentityAdministration
{
    public async Task<HumanUserStatus> CreateAsync(string username, string displayName, string password, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(username) || username.Length > 256 || string.IsNullOrWhiteSpace(displayName) || displayName.Length > 200)
            throw Invalid("User identity fields are invalid.");
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = username.Trim(),
            DisplayName = displayName.Trim(),
            CreatedAtUtc = clock.UtcNow,
            EmailConfirmed = true
        };
        var result = await users.CreateAsync(user, password);
        EnsureSucceeded(result, "identity.user_conflict");
        return await ToStatusAsync(user);
    }

    public async Task SetEnabledAsync(UserId userId, bool enabled, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await RequiredAsync(userId);
        user.LockoutEnabled = true; user.LockoutEnd = enabled ? null : DateTimeOffset.MaxValue;
        await users.UpdateSecurityStampAsync(user); EnsureSucceeded(await users.UpdateAsync(user), "identity.update_failed");
    }

    public async Task ResetPasswordAsync(UserId userId, string newPassword, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await RequiredAsync(userId);
        var token = await users.GeneratePasswordResetTokenAsync(user);
        EnsureSucceeded(await users.ResetPasswordAsync(user, token, newPassword), "identity.password_policy");
        user.MustChangePassword = true;
        await users.UpdateSecurityStampAsync(user); EnsureSucceeded(await users.UpdateAsync(user), "identity.update_failed");
    }

    public async Task AssignRoleAsync(UserId userId, string role, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateHumanRole(role);
        var user = await RequiredAsync(userId);
        if (!await users.IsInRoleAsync(user, role)) EnsureSucceeded(await users.AddToRoleAsync(user, role), "identity.role_failed");
    }

    public async Task RemoveRoleAsync(UserId userId, string role, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateHumanRole(role);
        var user = await RequiredAsync(userId);
        if (await users.IsInRoleAsync(user, role)) EnsureSucceeded(await users.RemoveFromRoleAsync(user, role), "identity.role_failed");
    }

    public async Task<HumanUserStatus> GetStatusAsync(UserId userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await ToStatusAsync(await RequiredAsync(userId));
    }

    private async Task<ApplicationUser> RequiredAsync(UserId id) => await users.FindByIdAsync(id.Value.ToString("D"))
        ?? throw new ApplicationFailure(ErrorCodes.NotFound, "User not found.");
    private async Task<HumanUserStatus> ToStatusAsync(ApplicationUser user) => new(new(user.Id), user.UserName ?? string.Empty,
        user.DisplayName, user.LockoutEnd is null || user.LockoutEnd <= clock.UtcNow, user.MustChangePassword,
        [.. await users.GetRolesAsync(user)]);
    private static void ValidateHumanRole(string role)
    {
        if (!MastemisRoles.All.Contains(role, StringComparer.Ordinal) || role == MastemisRoles.JudgeWorker)
            throw Invalid("The role cannot be assigned to a human identity.");
    }
    private static void EnsureSucceeded(IdentityResult result, string code)
    {
        if (!result.Succeeded) throw new ApplicationFailure(code, "The identity operation was rejected.");
    }
    private static ApplicationFailure Invalid(string message) => new(ErrorCodes.InvalidInput, message);
}
