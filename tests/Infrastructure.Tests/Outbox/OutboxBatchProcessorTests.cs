using Mastemis.Application;
using Mastemis.Infrastructure.Persistence;
using Mastemis.Infrastructure.Persistence.Outbox;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mastemis.Infrastructure.Tests.Outbox;

public sealed class OutboxBatchProcessorTests
{
    [Fact]
    public async Task Successful_publication_marks_message_processed_with_stable_identifier()
    {
        var message = Message(); var publisher = new RecordingPublisher();
        var processor = Create(message, publisher);

        await processor.ProcessAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(message.ProcessedAtUtc);
        Assert.Equal(message.Id.ToString("D"), Assert.Single(publisher.Ids));
    }

    [Fact]
    public async Task Transient_failures_are_scheduled_with_bounded_backoff_and_never_marked_processed()
    {
        var message = Message(); var processor = Create(message, new RecordingPublisher(fail: true));
        await processor.ProcessAsync(TestContext.Current.CancellationToken);
        Assert.Null(message.ProcessedAtUtc); Assert.Equal(1, message.Attempts);
        Assert.Equal("outbox.publish_failed", message.FailureCode);
        Assert.Equal(FixedClock.Now.AddSeconds(2), message.NextAttemptAtUtc);
    }

    [Fact]
    public async Task Tenth_failure_transitions_to_poison_state()
    {
        var message = Message(); message.Attempts = 9;
        await Create(message, new RecordingPublisher(fail: true)).ProcessAsync(TestContext.Current.CancellationToken);
        Assert.Equal(10, message.Attempts); Assert.Equal("outbox.poison", message.FailureCode);
    }

    [Fact]
    public async Task Cancellation_is_propagated_without_mutating_delivery_state()
    {
        var message = Message(); using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Create(message, new RecordingPublisher()).ProcessAsync(cancellation.Token));
        Assert.Null(message.ProcessedAtUtc); Assert.Equal(0, message.Attempts);
    }

    private static OutboxBatchProcessor Create(OutboxRow message, IOutboxPublisher publisher) => new(
        new MemoryStore(message), publisher, new FixedClock(), new OutboxStatus(), NullLogger<OutboxBatchProcessor>.Instance);
    private static OutboxRow Message() => new()
    {
        Id = Guid.NewGuid(),
        Type = "contract.v1",
        Payload = "{}",
        CreatedAtUtc = FixedClock.Now,
        NextAttemptAtUtc = FixedClock.Now
    };

    private sealed class MemoryStore(OutboxRow message) : IOutboxDeliveryStore
    {
        public async Task ProcessClaimedBatchAsync(DateTimeOffset now, int limit, Func<OutboxRow, CancellationToken, Task> process, CancellationToken cancellationToken)
        { cancellationToken.ThrowIfCancellationRequested(); if (message.ProcessedAtUtc is null && message.NextAttemptAtUtc <= now && message.Attempts < 10) await process(message, cancellationToken); }
    }
    private sealed class RecordingPublisher(bool fail = false) : IOutboxPublisher
    {
        public List<string> Ids { get; } = [];
        public Task PublishAsync(string messageId, string messageType, string payload, CancellationToken cancellationToken)
        { cancellationToken.ThrowIfCancellationRequested(); Ids.Add(messageId); return fail ? Task.FromException(new IOException("transient")) : Task.CompletedTask; }
    }
    private sealed class FixedClock : IClock
    {
        public static DateTimeOffset Now { get; } = new(2026, 7, 22, 6, 0, 0, TimeSpan.Zero);
        public DateTimeOffset UtcNow => Now;
    }
}
