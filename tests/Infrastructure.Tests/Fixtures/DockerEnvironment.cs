using System.Net;
using System.Net.Sockets;

namespace Mastemis.Infrastructure.Tests.Fixtures;

public static class DockerEnvironment
{
    public static bool IsAvailable()
    {
        try
        {
            using var socket = CreateSocket(out var endpoint);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            socket.ConnectAsync(endpoint, timeout.Token).AsTask().GetAwaiter().GetResult();
            return socket.Connected;
        }
        catch (Exception error) when (error is SocketException or OperationCanceledException or UriFormatException or NotSupportedException)
        {
            return false;
        }
    }

    private static Socket CreateSocket(out EndPoint endpoint)
    {
        var configured = Environment.GetEnvironmentVariable("DOCKER_HOST");
        if (string.IsNullOrWhiteSpace(configured) || configured.StartsWith("unix://", StringComparison.OrdinalIgnoreCase))
        {
            var path = string.IsNullOrWhiteSpace(configured) ? "/var/run/docker.sock" : configured[7..];
            endpoint = new UnixDomainSocketEndPoint(path);
            return new(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        }

        var uri = new Uri(configured);
        endpoint = new DnsEndPoint(uri.Host, uri.Port);
        return new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    }
}
