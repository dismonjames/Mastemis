namespace Mastemis.Domain;

public sealed class ExamSession
{
    private readonly List<ConfirmedWarning> _warnings = [];
    private readonly HashSet<ViolationEvaluationId> _warningEvaluations = [];

    private ExamSession() { }

    public ExamSession(SessionId id, ExamId examId, RoomId roomId, CandidateId candidateId)
    {
        Id = id;
        ExamId = examId;
        RoomId = roomId;
        CandidateId = candidateId;
    }

    public SessionId Id { get; private set; }
    public ExamId ExamId { get; private set; }
    public RoomId RoomId { get; private set; }
    public CandidateId CandidateId { get; private set; }
    public SessionState State { get; private set; } = SessionState.Pending;
    public DateTimeOffset? StartedAtUtc { get; private set; }
    public DateTimeOffset? TerminatedAtUtc { get; private set; }
    public SourceRevisionId? CurrentRevisionId { get; private set; }
    public SourceRevisionId? FrozenRevisionId { get; private set; }
    public int Version { get; private set; }
    public IReadOnlyList<ConfirmedWarning> Warnings => _warnings;

    public static ExamSession Restore(SessionId id, ExamId examId, RoomId roomId, CandidateId candidateId,
        SessionState state, DateTimeOffset? startedAtUtc, DateTimeOffset? terminatedAtUtc,
        SourceRevisionId? currentRevisionId, SourceRevisionId? frozenRevisionId, int version,
        IEnumerable<ConfirmedWarning> warnings)
    {
        var session = new ExamSession(id, examId, roomId, candidateId)
        {
            State = state,
            StartedAtUtc = startedAtUtc?.ToUniversalTime(),
            TerminatedAtUtc = terminatedAtUtc?.ToUniversalTime(),
            CurrentRevisionId = currentRevisionId,
            FrozenRevisionId = frozenRevisionId,
            Version = version
        };
        foreach (var warning in warnings.OrderBy(x => x.Ordinal))
        {
            session._warnings.Add(warning);
            session._warningEvaluations.Add(warning.EvaluationId);
        }
        return session;
    }

    public void Start(Exam exam, DateTimeOffset nowUtc)
    {
        if (State != SessionState.Pending) InvalidTransition(SessionState.Active);
        if (exam.Id != ExamId || exam.State != ExamState.Open)
            throw new DomainException("session.exam_not_open", "A session can start only when its exam is open.");
        State = SessionState.Active;
        StartedAtUtc = Exam.EnsureUtc(nowUtc);
        Version++;
    }

    public void Disconnect()
    {
        if (State != SessionState.Active) InvalidTransition(SessionState.Disconnected);
        State = SessionState.Disconnected;
        Version++;
    }

    public void Reconnect()
    {
        if (State != SessionState.Disconnected) InvalidTransition(SessionState.Active);
        State = SessionState.Active;
        Version++;
    }

    public void SaveRevision(SourceRevisionId revisionId)
    {
        EnsureWritable();
        CurrentRevisionId = revisionId;
        Version++;
    }

    public ConfirmedWarning? IssueWarning(ViolationEvaluation evaluation, DateTimeOffset nowUtc)
    {
        if (evaluation.SessionId != Id) throw new DomainException("warning.wrong_session", "Evaluation belongs to another session.");
        if (evaluation.Result != EvaluationResult.ConfirmedViolation)
            throw new DomainException("warning.not_confirmed", "Only confirmed violations create warnings.");
        if (_warningEvaluations.Contains(evaluation.Id)) return null;
        if (State is SessionState.Completed or SessionState.Terminated)
            throw new DomainException("session.final", "A final session cannot receive warnings.");

        var warning = new ConfirmedWarning(WarningId.New(), Id, evaluation.Id, _warnings.Count + 1, Exam.EnsureUtc(nowUtc));
        _warningEvaluations.Add(evaluation.Id);
        _warnings.Add(warning);
        Version++;
        return warning;
    }

    public void Terminate(DateTimeOffset nowUtc, SourceRevisionId frozenRevisionId)
    {
        if (_warnings.Count < 3) throw new DomainException("session.warning_threshold", "Three confirmed warnings are required.");
        if (State is SessionState.Terminated)
        {
            if (FrozenRevisionId != frozenRevisionId) throw new DomainException("session.already_terminated", "The frozen revision cannot change.");
            return;
        }
        if (State is SessionState.Completed or SessionState.Pending) InvalidTransition(SessionState.Terminated);
        State = SessionState.Terminated;
        TerminatedAtUtc = Exam.EnsureUtc(nowUtc);
        FrozenRevisionId = frozenRevisionId;
        Version++;
    }

    public void Complete()
    {
        if (State is not (SessionState.Active or SessionState.Disconnected)) InvalidTransition(SessionState.Completed);
        State = SessionState.Completed;
        Version++;
    }

    public void EnsureMaySubmit()
    {
        if (State is SessionState.Terminated or SessionState.Completed)
            throw new DomainException("session.submission_rejected", "Final sessions reject ordinary submissions.");
        if (State is not (SessionState.Active or SessionState.Disconnected))
            throw new DomainException("session.not_active", "The session is not active.");
    }

    private void EnsureWritable()
    {
        if (State is SessionState.Terminated or SessionState.Completed)
            throw new DomainException("session.draft_rejected", "Final sessions reject draft updates.");
        if (State is not (SessionState.Active or SessionState.Disconnected))
            throw new DomainException("session.not_active", "The session is not active.");
    }

    private void InvalidTransition(SessionState target) =>
        throw new DomainException("session.invalid_transition", $"Cannot transition session from {State} to {target}.");
}
