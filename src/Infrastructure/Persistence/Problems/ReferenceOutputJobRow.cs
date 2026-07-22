namespace Mastemis.Infrastructure.Persistence.Problems;

public sealed class ReferenceOutputJobRow
{
    public Guid Id { get; set; }
    public Guid OperationId { get; set; }
    public Guid ProblemId { get; set; }
    public string Language { get; set; } = string.Empty;
    public int ContractVersion { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
    public int Status { get; set; }
    public int Attempt { get; set; }
    public int MaximumAttempts { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset AvailableAtUtc { get; set; }
    public Guid? WorkerId { get; set; }
    public Guid? LeaseToken { get; set; }
    public DateTimeOffset? LeaseExpiresAtUtc { get; set; }
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public string? FailureCode { get; set; }
    public Guid ConcurrencyToken { get; set; }
}
