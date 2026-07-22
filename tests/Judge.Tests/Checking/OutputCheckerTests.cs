using System.Text;
using Mastemis.Contracts.Judge;
using Mastemis.Judge.Checking;

namespace Mastemis.Judge.Tests.Checking;

public sealed class OutputCheckerTests
{
    [Theory]
    [InlineData("answer\n", "answer\n", true)]
    [InlineData("answer\n", "answer", false)]
    [InlineData("answer\r\n", "answer\n", false)]
    [InlineData("answer", "answer ", false)]
    [InlineData("", "", true)]
    public async Task Exact_checker_compares_every_byte(string expected, string actual, bool accepted)
    {
        var result = await new ExactOutputChecker().CheckAsync(Request(expected, actual), TestContext.Current.CancellationToken);
        Assert.Equal(accepted, result.Accepted);
    }

    [Theory]
    [InlineData("1 2 3", "1\n2\t3 ", true, null)]
    [InlineData("1 2", "1 2 3", false, "checker.extra_token")]
    [InlineData("1 2 3", "1 2", false, "checker.missing_token")]
    [InlineData("héllo", "héllo", true, null)]
    [InlineData("", "", true, null)]
    [InlineData("", "extra", false, "checker.extra_token")]
    public async Task Token_checker_uses_strict_utf8_whitespace_tokens(string expected, string actual, bool accepted, string? code)
    {
        var result = await new TokenOutputChecker().CheckAsync(Request(expected, actual), TestContext.Current.CancellationToken);
        Assert.Equal(accepted, result.Accepted); Assert.Equal(code, result.Diagnostic?.Code);
    }

    [Fact]
    public async Task Token_checker_rejects_invalid_utf8_and_both_checkers_reject_oversize()
    {
        var invalid = await new TokenOutputChecker().CheckAsync(new(new byte[] { 0xff }, new byte[] { 0xff }, 10), TestContext.Current.CancellationToken);
        Assert.Equal("checker.invalid_utf8", invalid.Diagnostic?.Code);
        var oversize = new CheckerRequest(Encoding.UTF8.GetBytes("12"), Encoding.UTF8.GetBytes("12"), 1);
        Assert.False((await new ExactOutputChecker().CheckAsync(oversize, TestContext.Current.CancellationToken)).Accepted);
        Assert.False((await new TokenOutputChecker().CheckAsync(oversize, TestContext.Current.CancellationToken)).Accepted);
    }

    private static CheckerRequest Request(string expected, string actual) => new(
        Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual), 1024);
}
