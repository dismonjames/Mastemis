using System.Security.Claims;
using Mastemis.Infrastructure.Persistence;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;

namespace Mastemis.Server.Endpoints.Auth;

public static class AuthEndpoints
{
    public static void MapAuthenticationEndpoints(this WebApplication app)
    {
        app.MapGet("/api/auth/antiforgery", (IAntiforgery antiforgery, HttpContext context) =>
            Results.Ok(new { token = antiforgery.GetAndStoreTokens(context).RequestToken }));
        app.MapPost("/api/auth/login", async (LoginRequest request, SignInManager<ApplicationUser> signIn) =>
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["credentials"] = ["Username and password are required."] });
            var result = await signIn.PasswordSignInAsync(request.Username, request.Password, request.RememberMe, lockoutOnFailure: true);
            return result.Succeeded ? Results.NoContent() : result.IsLockedOut
                ? Results.Problem(statusCode: 423, title: "Account locked", extensions: new Dictionary<string, object?> { ["code"] = "identity.locked" })
                : Results.Problem(statusCode: 401, title: "Authentication failed", extensions: new Dictionary<string, object?> { ["code"] = "identity.invalid_credentials" });
        }).DisableAntiforgery();
        app.MapPost("/api/auth/logout", async (SignInManager<ApplicationUser> signIn) => { await signIn.SignOutAsync(); return Results.NoContent(); })
            .RequireAuthorization().WithMetadata(new RequireAntiforgeryTokenAttribute(true));
        app.MapGet("/api/auth/me", async (ClaimsPrincipal principal, UserManager<ApplicationUser> users) =>
        {
            var user = await users.GetUserAsync(principal);
            return user is null ? Results.Unauthorized() : Results.Ok(new
            {
                id = user.Id,
                username = user.UserName,
                displayName = user.DisplayName,
                roles = await users.GetRolesAsync(user)
            });
        }).RequireAuthorization();
    }
}

public sealed record LoginRequest(string Username, string Password, bool RememberMe);
