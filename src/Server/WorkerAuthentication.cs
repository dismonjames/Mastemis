using System.Security.Claims;
using System.Text.Encodings.Web;
using Mastemis.Application;
using Mastemis.Domain;
using Mastemis.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

public static class WorkerAuthenticationDefaults { public const string Scheme = "WorkerSecret"; }

public sealed class WorkerAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder, IWorkerCredentialService credentials)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Worker ", StringComparison.Ordinal)) return AuthenticateResult.NoResult();
        var parts = header[7..].Split('.', 2);
        if (parts.Length != 2 || !Guid.TryParse(parts[0], out var workerId)) return AuthenticateResult.Fail("Invalid worker credential.");
        if (!await credentials.AuthenticateAsync(new JudgeWorkerId(workerId), parts[1], Context.RequestAborted))
            return AuthenticateResult.Fail("Invalid worker credential.");
        var identity = new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, workerId.ToString("D")),
            new Claim(ClaimTypes.Role, MastemisRoles.JudgeWorker),
            new Claim("worker_id", workerId.ToString("D"))], Scheme.Name);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }
}
