using Mastemis.Domain;

namespace Mastemis.Application.Administration;

public sealed record HumanUserStatus(UserId UserId, string Username, string DisplayName, bool Enabled,
    bool MustChangePassword, IReadOnlyList<string> Roles);
public sealed record ScopeAssignment(Guid ResourceId, UserId UserId, string Role, DateTimeOffset AssignedAtUtc);

public interface IAdministrationActor
{
    UserId UserId { get; }
    bool IsInRole(string role);
}

public interface IHumanIdentityAdministration
{
    Task<HumanUserStatus> CreateAsync(string username, string displayName, string password, CancellationToken cancellationToken);
    Task SetEnabledAsync(UserId userId, bool enabled, CancellationToken cancellationToken);
    Task ResetPasswordAsync(UserId userId, string newPassword, CancellationToken cancellationToken);
    Task AssignRoleAsync(UserId userId, string role, CancellationToken cancellationToken);
    Task RemoveRoleAsync(UserId userId, string role, CancellationToken cancellationToken);
    Task<HumanUserStatus> GetStatusAsync(UserId userId, CancellationToken cancellationToken);
}

public interface IScopeAdministration
{
    Task<ScopeAssignment> AssignExamAsync(ExamId examId, UserId userId, string role, CancellationToken cancellationToken);
    Task RemoveExamAsync(ExamId examId, UserId userId, string role, CancellationToken cancellationToken);
    Task<IReadOnlyList<ScopeAssignment>> ListExamAsync(ExamId examId, CancellationToken cancellationToken);
    Task<ScopeAssignment> AssignRoomAsync(RoomId roomId, UserId userId, CancellationToken cancellationToken);
    Task RemoveRoomAsync(RoomId roomId, UserId userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ScopeAssignment>> ListRoomAsync(RoomId roomId, CancellationToken cancellationToken);
}
