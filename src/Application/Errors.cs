namespace Mastemis.Application;

public static class ErrorCodes
{
    public const string NotFound = "resource.not_found";
    public const string Forbidden = "authorization.forbidden";
    public const string InvalidInput = "request.invalid";
    public const string IdempotencyConflict = "idempotency.conflict";
    public const string ConcurrencyConflict = "persistence.concurrency_conflict";
    public const string LeaseRejected = "judge.lease_rejected";
}

public sealed class ApplicationFailure(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
