using Mastemis.Contracts.Judge;

namespace Mastemis.Judge.Checking;

public sealed class ExactOutputChecker : IOutputChecker
{
    public string CheckerId => "exact";
    public ValueTask<CheckerResult> CheckAsync(CheckerRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.MaximumBytes < 0 || request.Expected.Length > request.MaximumBytes || request.Actual.Length > request.MaximumBytes)
            return ValueTask.FromResult(Failed("checker.output_limit", "Checker input exceeded its configured byte limit."));
        if (request.Expected.Span.SequenceEqual(request.Actual.Span)) return ValueTask.FromResult(new CheckerResult(true, null));
        var first = FirstDifference(request.Expected.Span, request.Actual.Span);
        return ValueTask.FromResult(Failed("checker.exact_mismatch", $"Output differs at byte offset {first}."));
    }
    private static int FirstDifference(ReadOnlySpan<byte> expected, ReadOnlySpan<byte> actual)
    {
        var length = Math.Min(expected.Length, actual.Length);
        for (var index = 0; index < length; index++) if (expected[index] != actual[index]) return index;
        return length;
    }
    private static CheckerResult Failed(string code, string message) => new(false, new(code, message, JudgeDiagnosticSeverity.Error));
}
