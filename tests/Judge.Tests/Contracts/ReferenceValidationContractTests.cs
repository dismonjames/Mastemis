using Mastemis.Contracts.Judge;
using Mastemis.Contracts.Problems.ReferenceValidation;
using Mastemis.Domain;

namespace Mastemis.Judge.Tests.Contracts;

public sealed class ReferenceValidationContractTests
{
    [Fact]
    public void Valid_payload_is_accepted() => ValidPayload().Validate();

    [Theory]
    [InlineData("../main.cpp")]
    [InlineData("folder/main.cpp")]
    [InlineData("")]
    public void Unsafe_logical_file_is_rejected(string name)
    {
        var payload = ValidPayload() with
        {
            Sources = [new(name, "object", new string('a', 64), 12)]
        };

        Assert.Throws<JudgeContractException>(payload.Validate);
    }

    [Fact]
    public void Oversized_diagnostics_are_rejected()
    {
        var result = ValidResult() with
        {
            Diagnostics = [new(ReferenceDiagnosticSeverity.Error, "E1",
                new string('x', ReferenceValidationResult.MaximumDiagnosticMessageLength + 1), "main.cpp", 1, 1)]
        };

        Assert.Throws<JudgeContractException>(() => result.Validate(new HashSet<string>(["main.cpp"], StringComparer.Ordinal)));
    }

    [Fact]
    public void Unknown_diagnostic_file_is_rejected()
    {
        var result = ValidResult() with
        {
            Diagnostics = [new(ReferenceDiagnosticSeverity.Error, null, "error", "other.cpp", 1, 1)]
        };

        Assert.Throws<JudgeContractException>(() => result.Validate(new HashSet<string>(["main.cpp"], StringComparer.Ordinal)));
    }

    private static ReferenceValidationJobPayload ValidPayload() => new(
        ReferenceValidationJobPayload.CurrentVersion, Guid.NewGuid(), ProblemId.New(), Guid.NewGuid(), "cpp",
        [new("main.cpp", "object", new string('a', 64), 12)], "cpp23-o2-v1", UserId.New(),
        DateTimeOffset.Parse("2026-07-23T00:00:00Z"), TimeSpan.FromSeconds(30));

    private static ReferenceValidationResult ValidResult() => new(
        ReferenceValidationResult.CurrentVersion, Guid.NewGuid(), Guid.NewGuid(), JudgeWorkerId.New(), Guid.NewGuid(),
        ReferenceValidationStatus.Failed, "clang++", "20", ReferenceValidationExitClassification.CompilationError,
        [], 42, "trusted-worker", null, DateTimeOffset.Parse("2026-07-23T00:00:01Z"), "compilation-error");
}
