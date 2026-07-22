using System.Text;
using Mastemis.Contracts.Judge;

namespace Mastemis.Judge.Checking;

public sealed class TokenOutputChecker : IOutputChecker
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    public string CheckerId => "tokens";

    public ValueTask<CheckerResult> CheckAsync(CheckerRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.MaximumBytes < 0 || request.Expected.Length > request.MaximumBytes || request.Actual.Length > request.MaximumBytes)
            return ValueTask.FromResult(Failed("checker.output_limit", "Checker input exceeded its configured byte limit."));
        string expected; string actual;
        try { expected = StrictUtf8.GetString(request.Expected.Span); actual = StrictUtf8.GetString(request.Actual.Span); }
        catch (DecoderFallbackException) { return ValueTask.FromResult(Failed("checker.invalid_utf8", "Token output must be valid UTF-8.")); }
        var expectedTokens = expected.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var actualTokens = actual.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var count = Math.Min(expectedTokens.Length, actualTokens.Length);
        for (var index = 0; index < count; index++)
            if (!string.Equals(expectedTokens[index], actualTokens[index], StringComparison.Ordinal))
                return ValueTask.FromResult(Failed("checker.token_mismatch", $"Token {index + 1} differs."));
        if (actualTokens.Length > expectedTokens.Length) return ValueTask.FromResult(Failed("checker.extra_token", $"Unexpected token {expectedTokens.Length + 1}."));
        if (actualTokens.Length < expectedTokens.Length) return ValueTask.FromResult(Failed("checker.missing_token", $"Missing token {actualTokens.Length + 1}."));
        return ValueTask.FromResult(new CheckerResult(true, null));
    }
    private static CheckerResult Failed(string code, string message) => new(false, new(code, message, JudgeDiagnosticSeverity.Error));
}
