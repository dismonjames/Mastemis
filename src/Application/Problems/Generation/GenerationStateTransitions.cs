using Mastemis.Application.Problems.Authoring;

namespace Mastemis.Application.Problems.Generation;

public static class GenerationStateTransitions
{
    public static bool CanTransition(GenerationOperationStatus from, GenerationOperationStatus to) => (from, to) switch
    {
        (GenerationOperationStatus.Pending, GenerationOperationStatus.Validating) => true,
        (GenerationOperationStatus.Validating, GenerationOperationStatus.GeneratingInputs) => true,
        (GenerationOperationStatus.GeneratingInputs, GenerationOperationStatus.WaitingForReferenceOutputs) => true,
        (GenerationOperationStatus.WaitingForReferenceOutputs, GenerationOperationStatus.Publishing) => true,
        (GenerationOperationStatus.Publishing, GenerationOperationStatus.Completed) => true,
        (_, GenerationOperationStatus.Failed) when !IsTerminal(from) => true,
        (_, GenerationOperationStatus.CancelRequested) when !IsTerminal(from) => true,
        (GenerationOperationStatus.CancelRequested, GenerationOperationStatus.Cancelled) => true,
        _ => false
    };

    public static bool IsTerminal(GenerationOperationStatus state) =>
        state is GenerationOperationStatus.Completed or GenerationOperationStatus.Failed or GenerationOperationStatus.Cancelled;
}
