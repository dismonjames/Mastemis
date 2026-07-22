namespace Mastemis.Client.Core.Session;

public enum ClientMode { Host, Connect }

public sealed class ClientSession
{
    private readonly HashSet<string> roles = new(StringComparer.OrdinalIgnoreCase);
    public Uri? ServerBaseUri { get; private set; }
    public ClientMode Mode { get; private set; } = ClientMode.Connect;
    public AuthenticatedUser? User { get; private set; }
    public bool IsAuthenticated => User is not null;
    public IReadOnlySet<string> Roles => roles;
    public event EventHandler? Changed;
    public void SelectServer(Uri serverBaseUri, ClientMode mode) => (ServerBaseUri, Mode) = (serverBaseUri, mode);

    public void Authenticate(AuthenticatedUser user)
    {
        User = user;
        roles.Clear();
        foreach (var role in user.Roles) roles.Add(role);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SignOut()
    {
        User = null;
        roles.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool HasAnyRole(params string[] required) => required.Any(roles.Contains);
}

public sealed record AuthenticatedUser(Guid Id, string Username, string? DisplayName, IReadOnlyList<string> Roles);
