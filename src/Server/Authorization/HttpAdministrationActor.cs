using System.Security.Claims;
using Mastemis.Application;
using Mastemis.Application.Administration;
using Mastemis.Domain;

namespace Mastemis.Server.Authorization;

public sealed class HttpAdministrationActor(IHttpContextAccessor accessor) : IAdministrationActor
{
    private ClaimsPrincipal Principal => accessor.HttpContext?.User
        ?? throw new ApplicationFailure(ErrorCodes.Forbidden, "An authenticated administrator is required.");
    public UserId UserId => Guid.TryParse(Principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? new(id) : throw new ApplicationFailure(ErrorCodes.Forbidden, "An authenticated administrator is required.");
    public bool IsInRole(string role) => Principal.IsInRole(role);
}
