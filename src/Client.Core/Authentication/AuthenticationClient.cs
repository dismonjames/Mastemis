using System.Net.Http.Json;
using Mastemis.Client.Core.Networking.Http;
using Mastemis.Client.Core.Session;

namespace Mastemis.Client.Core.Authentication;

public interface IAuthenticationClient
{
    Task<AuthenticatedUser> LoginAsync(string username, string password, bool rememberMe, CancellationToken cancellationToken);
    Task<AuthenticatedUser?> GetCurrentUserAsync(CancellationToken cancellationToken);
    Task LogoutAsync(CancellationToken cancellationToken);
}

public sealed class AuthenticationClient(HttpClient http, IApiTransport transport, ClientSession session) : IAuthenticationClient
{
    public async Task<AuthenticatedUser> LoginAsync(string username, string password, bool rememberMe, CancellationToken cancellationToken)
    {
        var baseUri = session.ServerBaseUri ?? throw new InvalidOperationException("No Mastemis server is selected.");
        using var response = await http.PostAsJsonAsync(new Uri(baseUri, "/api/auth/login"), new { username, password, rememberMe }, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) throw new ApiException(new(response.StatusCode, "Authentication failed", null, "identity.invalid_credentials", null, new Dictionary<string, string[]>()));
        var user = await transport.GetAsync<AuthenticatedUser>("/api/auth/me", cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("The server did not return the authenticated user.");
        session.Authenticate(user);
        return user;
    }

    public async Task<AuthenticatedUser?> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        try
        {
            var user = await transport.GetAsync<AuthenticatedUser>("/api/auth/me", cancellationToken).ConfigureAwait(false);
            if (user is not null) session.Authenticate(user);
            return user;
        }
        catch (ApiException error) when ((int)error.Problem.Status == 401) { session.SignOut(); return null; }
    }

    public async Task LogoutAsync(CancellationToken cancellationToken)
    {
        await transport.SendAsync(HttpMethod.Post, "/api/auth/logout", new { }, null, cancellationToken).ConfigureAwait(false);
        session.SignOut();
    }
}
