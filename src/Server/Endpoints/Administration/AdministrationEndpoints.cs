using Mastemis.Application.Administration;
using Mastemis.Domain;

namespace Mastemis.Server.Endpoints.Administration;

public static class AdministrationEndpoints
{
    public static void MapAdministrationEndpoints(this WebApplication app)
    {
        var users = app.MapGroup("/api/admin/users").RequireAuthorization("Administrator");
        users.MapPost("/", async (CreateUserRequest request, IHumanIdentityAdministration service, CancellationToken ct) =>
            Results.Created("/api/admin/users", await service.CreateAsync(request.Username, request.DisplayName, request.Password, ct)));
        users.MapGet("/{userId:guid}", async (Guid userId, IHumanIdentityAdministration service, CancellationToken ct) =>
            await service.GetStatusAsync(new(userId), ct));
        users.MapPost("/{userId:guid}/disable", async (Guid userId, IHumanIdentityAdministration service, CancellationToken ct) =>
        { await service.SetEnabledAsync(new(userId), false, ct); return Results.NoContent(); });
        users.MapPost("/{userId:guid}/enable", async (Guid userId, IHumanIdentityAdministration service, CancellationToken ct) =>
        { await service.SetEnabledAsync(new(userId), true, ct); return Results.NoContent(); });
        users.MapPost("/{userId:guid}/password-reset", async (Guid userId, ResetPasswordRequest request, IHumanIdentityAdministration service, CancellationToken ct) =>
        { await service.ResetPasswordAsync(new(userId), request.NewPassword, ct); return Results.NoContent(); });
        users.MapPut("/{userId:guid}/roles/{role}", async (Guid userId, string role, IHumanIdentityAdministration service, CancellationToken ct) =>
        { await service.AssignRoleAsync(new(userId), role, ct); return Results.NoContent(); });
        users.MapDelete("/{userId:guid}/roles/{role}", async (Guid userId, string role, IHumanIdentityAdministration service, CancellationToken ct) =>
        { await service.RemoveRoleAsync(new(userId), role, ct); return Results.NoContent(); });

        var scopes = app.MapGroup("/api/scopes").RequireAuthorization();
        scopes.MapPut("/exams/{examId:guid}/users/{userId:guid}/{role}", async (Guid examId, Guid userId, string role, IScopeAdministration service, CancellationToken ct) =>
            await service.AssignExamAsync(new(examId), new(userId), role, ct));
        scopes.MapDelete("/exams/{examId:guid}/users/{userId:guid}/{role}", async (Guid examId, Guid userId, string role, IScopeAdministration service, CancellationToken ct) =>
        { await service.RemoveExamAsync(new(examId), new(userId), role, ct); return Results.NoContent(); });
        scopes.MapGet("/exams/{examId:guid}", async (Guid examId, IScopeAdministration service, CancellationToken ct) => await service.ListExamAsync(new(examId), ct));
        scopes.MapPut("/rooms/{roomId:guid}/users/{userId:guid}", async (Guid roomId, Guid userId, IScopeAdministration service, CancellationToken ct) => await service.AssignRoomAsync(new(roomId), new(userId), ct));
        scopes.MapDelete("/rooms/{roomId:guid}/users/{userId:guid}", async (Guid roomId, Guid userId, IScopeAdministration service, CancellationToken ct) =>
        { await service.RemoveRoomAsync(new(roomId), new(userId), ct); return Results.NoContent(); });
        scopes.MapGet("/rooms/{roomId:guid}", async (Guid roomId, IScopeAdministration service, CancellationToken ct) => await service.ListRoomAsync(new(roomId), ct));
    }
}

public sealed record CreateUserRequest(string Username, string DisplayName, string Password);
public sealed record ResetPasswordRequest(string NewPassword);
