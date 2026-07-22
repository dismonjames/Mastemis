using System.Security.Claims;
using Mastemis.Application;
using Mastemis.Application.Evidence;
using Mastemis.Domain;

namespace Mastemis.Server.Authorization;

public sealed class HttpEvidenceActor(IHttpContextAccessor accessor) : IEvidenceActor
{
    private ClaimsPrincipal Principal => accessor.HttpContext?.User ?? throw Denied();
    public UserId UserId => Guid.TryParse(Principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? new(id) : throw Denied();
    public bool IsInRole(string role) => Principal.IsInRole(role);
    private static ApplicationFailure Denied() => new(ErrorCodes.Forbidden, "An authenticated evidence reviewer is required.");
}
