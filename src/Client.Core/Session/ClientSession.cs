namespace Mastemis.Client.Core.Session;

public enum ClientMode { Host, Connect }

public sealed class ClientSession
{
    public Uri? ServerBaseUri { get; private set; }
    public ClientMode Mode { get; private set; } = ClientMode.Connect;
    public void SelectServer(Uri serverBaseUri, ClientMode mode) => (ServerBaseUri, Mode) = (serverBaseUri, mode);
}
