using Mastemis.Application;

namespace Mastemis.Server.Realtime;

public static class RealtimeContractCatalog
{
    public static bool IsSupported(string type) => type == typeof(CandidateConnected).FullName ||
        type == typeof(CandidateDisconnected).FullName || type == typeof(DraftSaved).FullName ||
        type == typeof(SubmissionCreated).FullName || type == typeof(JudgementUpdated).FullName ||
        type == typeof(SfeEventReceived).FullName || type == typeof(SfeEvaluationCreated).FullName ||
        type == typeof(WarningIssued).FullName || type == typeof(SessionTerminated).FullName ||
        type == typeof(WorkerConnected).FullName || type == typeof(WorkerDisconnected).FullName ||
        type == typeof(WorkerCapacityChanged).FullName || type == typeof(JudgeJobQueued).FullName ||
        type == typeof(JudgeJobClaimed).FullName || type == typeof(JudgeJobCompleted).FullName;
}
