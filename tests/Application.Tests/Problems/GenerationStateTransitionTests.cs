using Mastemis.Application.Problems.Authoring;
using Mastemis.Application.Problems.Generation;

namespace Mastemis.Application.Tests.Problems;

public sealed class GenerationStateTransitionTests
{
    [Theory]
    [InlineData(GenerationOperationStatus.Pending, GenerationOperationStatus.Validating)]
    [InlineData(GenerationOperationStatus.Validating, GenerationOperationStatus.GeneratingInputs)]
    [InlineData(GenerationOperationStatus.GeneratingInputs, GenerationOperationStatus.WaitingForReferenceOutputs)]
    [InlineData(GenerationOperationStatus.WaitingForReferenceOutputs, GenerationOperationStatus.Publishing)]
    [InlineData(GenerationOperationStatus.Publishing, GenerationOperationStatus.Completed)]
    public void Allows_forward_generation_pipeline(GenerationOperationStatus from, GenerationOperationStatus to) =>
        Assert.True(GenerationStateTransitions.CanTransition(from, to));

    [Fact]
    public void Rejects_skips_and_terminal_reentry()
    {
        Assert.False(GenerationStateTransitions.CanTransition(GenerationOperationStatus.Pending, GenerationOperationStatus.Completed));
        Assert.False(GenerationStateTransitions.CanTransition(GenerationOperationStatus.Completed, GenerationOperationStatus.Failed));
        Assert.True(GenerationStateTransitions.CanTransition(GenerationOperationStatus.GeneratingInputs, GenerationOperationStatus.CancelRequested));
        Assert.True(GenerationStateTransitions.CanTransition(GenerationOperationStatus.CancelRequested, GenerationOperationStatus.Cancelled));
    }
}
