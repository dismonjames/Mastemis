using System.Collections.Concurrent;
using Mastemis.Client.Core.Session;
using Microsoft.AspNetCore.SignalR.Client;

namespace Mastemis.Client.Core.Networking.Realtime;

public sealed record RealtimeEnvelope(string MessageId, int Version, string MessageType, DateTimeOffset OccurredAtUtc, string Payload);
public enum RealtimeConnectionState { Disconnected, Connecting, Connected, Reconnecting }

public interface IUiDispatcher { Task DispatchAsync(Func<Task> action, CancellationToken cancellationToken); }
public sealed class ImmediateUiDispatcher : IUiDispatcher { public Task DispatchAsync(Func<Task> action, CancellationToken cancellationToken) => action(); }

public sealed class RealtimeClient(ClientSession session, IUiDispatcher dispatcher) : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, byte> seen = new(StringComparer.Ordinal);
    private readonly List<(string Method, Guid Id)> groups = [];
    private HubConnection? connection;
    public RealtimeConnectionState State { get; private set; }
    public event EventHandler<RealtimeEnvelope>? EventReceived;
    public event EventHandler? StateChanged;

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (connection is not null) return;
        State = RealtimeConnectionState.Connecting; StateChanged?.Invoke(this, EventArgs.Empty);
        connection = new HubConnectionBuilder().WithUrl(new Uri(session.ServerBaseUri ?? throw new InvalidOperationException("No server selected."), "/hubs/exam"))
            .WithAutomaticReconnect([TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15)]).Build();
        connection.On<RealtimeEnvelope>("mastemis.event", envelope => ProcessEnvelopeAsync(envelope, CancellationToken.None));
        connection.Reconnecting += _ => { State = RealtimeConnectionState.Reconnecting; StateChanged?.Invoke(this, EventArgs.Empty); return Task.CompletedTask; };
        connection.Reconnected += async _ => { State = RealtimeConnectionState.Connected; StateChanged?.Invoke(this, EventArgs.Empty); await RejoinAsync(CancellationToken.None).ConfigureAwait(false); };
        connection.Closed += _ => { State = RealtimeConnectionState.Disconnected; StateChanged?.Invoke(this, EventArgs.Empty); return Task.CompletedTask; };
        await connection.StartAsync(cancellationToken).ConfigureAwait(false);
        State = RealtimeConnectionState.Connected; StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task JoinAsync(string scope, Guid id, CancellationToken cancellationToken)
    {
        var method = scope switch { "exam" => "JoinExam", "room" => "JoinRoom", "candidate" => "JoinCandidate", "chief" => "JoinChief", "problem" => "JoinProblem", "worker" => "JoinWorker", _ => throw new ArgumentOutOfRangeException(nameof(scope)) };
        if (!groups.Contains((method, id))) groups.Add((method, id));
        if (connection?.State == HubConnectionState.Connected) await connection.InvokeAsync(method, id.ToString("D"), cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (connection is not null) { await connection.DisposeAsync().ConfigureAwait(false); connection = null; }
        State = RealtimeConnectionState.Disconnected;
    }

    public async Task ProcessEnvelopeAsync(RealtimeEnvelope envelope, CancellationToken cancellationToken)
    {
        if (envelope.Version != 1 || !seen.TryAdd(envelope.MessageId, 0)) return;
        if (seen.Count > 4096) foreach (var key in seen.Keys.Take(1024)) seen.TryRemove(key, out _);
        await dispatcher.DispatchAsync(() => { EventReceived?.Invoke(this, envelope); return Task.CompletedTask; }, cancellationToken).ConfigureAwait(false);
    }

    private async Task RejoinAsync(CancellationToken cancellationToken)
    {
        if (connection is null) return;
        foreach (var group in groups) await connection.InvokeAsync(group.Method, group.Id.ToString("D"), cancellationToken).ConfigureAwait(false);
    }
}
