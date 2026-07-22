using Mastemis.Domain;

namespace Mastemis.Application;

public sealed class MastemisService(
    IAggregateStore store,
    IUnitOfWork unitOfWork,
    IClock clock,
    IAuthorizationService authorization,
    ISourceRevisionStorage sourceStorage,
    IDurableJudgeQueue judgeQueue,
    ITransactionalOutbox outbox)
{
    public async Task<Exam> CreateExamAsync(CreateExamCommand command, CancellationToken cancellationToken)
    {
        ValidateKey(command.IdempotencyKey);
        await authorization.EnsureAsync("exam.create", Guid.Empty, cancellationToken);
        return await unitOfWork.ExecuteAsync(async ct =>
        {
            await EnsureNewKeyAsync(command.IdempotencyKey, ct);
            var exam = new Exam(ExamId.New(), command.Title, clock.UtcNow);
            await store.AddExamAsync(exam, ct);
            await store.AddIdempotencyKeyAsync(command.IdempotencyKey, ct);
            return exam;
        }, cancellationToken);
    }

    public async Task ScheduleExamAsync(ScheduleExamCommand command, CancellationToken cancellationToken)
    {
        await authorization.EnsureAsync("exam.manage", command.ExamId.Value, cancellationToken);
        await unitOfWork.ExecuteAsync(async ct =>
        {
            await EnsureNewKeyAsync(command.IdempotencyKey, ct);
            var exam = await RequiredExamAsync(command.ExamId, ct);
            exam.Schedule(command.StartsAtUtc, command.EndsAtUtc);
            await store.AddIdempotencyKeyAsync(command.IdempotencyKey, ct);
            return true;
        }, cancellationToken);
    }

    public Task OpenExamAsync(ExamId id, CancellationToken cancellationToken) =>
        ChangeExamAsync(id, "exam.manage", static (exam, now) => exam.Open(now), cancellationToken);

    public Task CloseExamAsync(ExamId id, CancellationToken cancellationToken) =>
        ChangeExamAsync(id, "exam.manage", static (exam, now) => exam.Close(now), cancellationToken);

    public Task CancelExamAsync(ExamId id, CancellationToken cancellationToken) =>
        ChangeExamAsync(id, "exam.manage", static (exam, now) => exam.Cancel(now), cancellationToken);

    public async Task<ExamRoom> CreateRoomAsync(CreateRoomCommand command, CancellationToken cancellationToken)
    {
        await authorization.EnsureAsync("room.create", command.ExamId.Value, cancellationToken);
        return await unitOfWork.ExecuteAsync(async ct =>
        {
            await EnsureNewKeyAsync(command.IdempotencyKey, ct);
            _ = await RequiredExamAsync(command.ExamId, ct);
            if (string.IsNullOrWhiteSpace(command.Name)) throw new ApplicationFailure(ErrorCodes.InvalidInput, "Room name is required.");
            var room = new ExamRoom(RoomId.New(), command.ExamId, command.Name.Trim());
            await store.AddRoomAsync(room, ct);
            await store.AddIdempotencyKeyAsync(command.IdempotencyKey, ct);
            return room;
        }, cancellationToken);
    }

    public async Task<Candidate> RegisterCandidateAsync(RegisterCandidateCommand command, CancellationToken cancellationToken)
    {
        await authorization.EnsureAsync("candidate.register", command.ExamId.Value, cancellationToken);
        return await unitOfWork.ExecuteAsync(async ct =>
        {
            await EnsureNewKeyAsync(command.IdempotencyKey, ct);
            _ = await RequiredExamAsync(command.ExamId, ct);
            if (string.IsNullOrWhiteSpace(command.RegistrationCode)) throw new ApplicationFailure(ErrorCodes.InvalidInput, "Registration code is required.");
            var candidate = new Candidate(CandidateId.New(), command.UserId, command.RegistrationCode.Trim());
            await store.AddCandidateAsync(candidate, command.ExamId, ct);
            await store.AddIdempotencyKeyAsync(command.IdempotencyKey, ct);
            return candidate;
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<Candidate>> ImportCandidatesAsync(ExamId examId,
        IReadOnlyList<(UserId UserId, string RegistrationCode)> candidates, string idempotencyKey, CancellationToken cancellationToken)
    {
        if (candidates.Count is 0 or > 10_000) throw new ApplicationFailure(ErrorCodes.InvalidInput, "Import must contain between 1 and 10,000 candidates.");
        var result = new List<Candidate>(candidates.Count);
        for (var index = 0; index < candidates.Count; index++)
        {
            var item = candidates[index];
            result.Add(await RegisterCandidateAsync(new(examId, item.UserId, item.RegistrationCode, $"{idempotencyKey}:{index}"), cancellationToken));
        }
        return result;
    }

    public async Task<ExamSession> StartExamSessionAsync(StartSessionCommand command, CancellationToken cancellationToken)
    {
        await authorization.EnsureAsync("session.start", command.CandidateId.Value, cancellationToken);
        return await unitOfWork.ExecuteAsync(async ct =>
        {
            await EnsureNewKeyAsync(command.IdempotencyKey, ct);
            var exam = await RequiredExamAsync(command.ExamId, ct);
            var room = await store.GetRoomAsync(command.RoomId, ct);
            if (room is null || room.ExamId != command.ExamId) throw NotFound("room");
            var session = new ExamSession(SessionId.New(), command.ExamId, command.RoomId, command.CandidateId);
            session.Start(exam, clock.UtcNow);
            await store.AddSessionAsync(session, ct);
            await store.AddIdempotencyKeyAsync(command.IdempotencyKey, ct);
            return session;
        }, cancellationToken);
    }

    public async Task ReconnectExamSessionAsync(SessionId id, CancellationToken cancellationToken)
    {
        await authorization.EnsureAsync("session.access", id.Value, cancellationToken);
        await unitOfWork.ExecuteAsync(async ct => { (await RequiredSessionAsync(id, ct)).Reconnect(); return true; }, cancellationToken);
    }

    public async Task<SourceRevisionMetadata> SaveDraftRevisionAsync(SaveDraftCommand command, CancellationToken cancellationToken)
    {
        await authorization.EnsureAsync("session.write", command.SessionId.Value, cancellationToken);
        if (command.Content.IsEmpty || command.Content.Length > 1_048_576)
            throw new ApplicationFailure(ErrorCodes.InvalidInput, "Source must contain 1 to 1,048,576 bytes.");
        return await unitOfWork.ExecuteAsync(async ct =>
        {
            await EnsureNewKeyAsync(command.IdempotencyKey, ct);
            var session = await RequiredSessionAsync(command.SessionId, ct);
            var revisionId = SourceRevisionId.New();
            var stored = await sourceStorage.StoreAsync(revisionId, command.Content, ct);
            var revision = new SourceRevisionMetadata(revisionId, command.SessionId, stored.ObjectId, stored.Sha256, clock.UtcNow, stored.Length);
            session.SaveRevision(revisionId);
            await store.AddRevisionAsync(revision, ct);
            await outbox.AddAsync(new DraftSaved(command.SessionId, revisionId), ct);
            await store.AddIdempotencyKeyAsync(command.IdempotencyKey, ct);
            return revision;
        }, cancellationToken);
    }

    public async Task<Submission> CreateSubmissionAsync(CreateSubmissionCommand command, CancellationToken cancellationToken)
    {
        await authorization.EnsureAsync("submission.create", command.SessionId.Value, cancellationToken);
        return await unitOfWork.ExecuteAsync(async ct =>
        {
            await EnsureNewKeyAsync(command.IdempotencyKey, ct);
            var session = await RequiredSessionAsync(command.SessionId, ct);
            session.EnsureMaySubmit();
            var submission = new Submission(SubmissionId.New(), command.SessionId, command.ProblemId,
                command.RevisionId, command.Language, clock.UtcNow);
            await store.AddSubmissionAsync(submission, ct);
            var job = new JudgeJob(JudgeJobId.New(), submission.Id, JudgeJobState.Pending, 0, clock.UtcNow);
            await store.AddJudgeJobAsync(job, ct);
            await judgeQueue.EnqueueAsync(job, ct);
            await outbox.AddAsync(new SubmissionCreated(command.SessionId, submission.Id, false), ct);
            await outbox.AddAsync(new JudgeJobQueued(job.Id, submission.Id), ct);
            await store.AddIdempotencyKeyAsync(command.IdempotencyKey, ct);
            return submission;
        }, cancellationToken);
    }

    public async Task<Submission> GetSubmissionAsync(SubmissionId id, CancellationToken cancellationToken)
    {
        await authorization.EnsureAsync("submission.read", id.Value, cancellationToken);
        return await store.GetSubmissionAsync(id, cancellationToken) ?? throw NotFound("submission");
    }

    public Task<IReadOnlyList<Submission>> GetSubmissionHistoryAsync(SessionId id, CancellationToken cancellationToken) =>
        store.GetSubmissionsAsync(id, cancellationToken);

    public async Task<ViolationEvent> RecordRawSfeEventAsync(RecordSfeEventCommand command, CancellationToken cancellationToken)
    {
        await authorization.EnsureAsync("sfe.record", command.SessionId.Value, cancellationToken);
        if (command.ClientSequence < 0 || string.IsNullOrWhiteSpace(command.EventType) || command.EventType.Length > 100)
            throw new ApplicationFailure(ErrorCodes.InvalidInput, "Invalid SFE event.");
        return await unitOfWork.ExecuteAsync(async ct =>
        {
            await EnsureNewKeyAsync(command.IdempotencyKey, ct);
            _ = await RequiredSessionAsync(command.SessionId, ct);
            var activityEvent = new ViolationEvent(ViolationEventId.New(), command.SessionId, command.ClientSequence,
                command.ClientTimestamp, clock.UtcNow, command.EventType.Trim(), command.Metadata);
            await store.AddEventAsync(activityEvent, ct);
            await outbox.AddAsync(new SfeEventReceived(command.SessionId, activityEvent.Id), ct);
            await store.AddIdempotencyKeyAsync(command.IdempotencyKey, ct);
            return activityEvent;
        }, cancellationToken);
    }

    public async Task<ViolationEvaluation> EvaluateSfeEventAsync(EvaluateSfeEventCommand command, CancellationToken cancellationToken)
    {
        var activityEvent = await store.GetEventAsync(command.EventId, cancellationToken) ?? throw NotFound("event");
        await authorization.EnsureAsync("sfe.evaluate", activityEvent.SessionId.Value, cancellationToken);
        return await unitOfWork.ExecuteAsync(async ct =>
        {
            var result = command.ConcurrentSession ? EvaluationResult.ConfirmedViolation
                : activityEvent.EventType == "PageHidden" && command.Duration >= TimeSpan.FromSeconds(30) ? EvaluationResult.ConfirmedViolation
                : activityEvent.EventType == "WindowBlurred" && command.Duration < TimeSpan.FromSeconds(2) ? EvaluationResult.Ignored
                : activityEvent.EventType is "ConnectionLost" or "BrowserCrash" ? EvaluationResult.Recorded
                : EvaluationResult.Suspected;
            var evaluation = new ViolationEvaluation(ViolationEvaluationId.New(), activityEvent.Id, activityEvent.SessionId,
                result, $"baseline.{result.ToString().ToLowerInvariant()}", clock.UtcNow);
            await store.AddEvaluationAsync(evaluation, ct);
            await outbox.AddAsync(new SfeEvaluationCreated(activityEvent.SessionId, evaluation.Id, evaluation.Result), ct);
            return evaluation;
        }, cancellationToken);
    }

    public async Task<ConfirmedWarning> ConfirmViolationAsync(ConfirmViolationCommand command, ViolationEvaluation evaluation,
        CancellationToken cancellationToken)
    {
        await authorization.EnsureAsync("warning.issue", command.SessionId.Value, cancellationToken);
        return await unitOfWork.ExecuteAsync(async ct =>
        {
            var session = await RequiredSessionAsync(command.SessionId, ct);
            var warning = session.IssueWarning(evaluation, clock.UtcNow);
            if (warning is null) throw new ApplicationFailure(ErrorCodes.IdempotencyConflict, "This evaluation already produced a warning.");
            await store.AddWarningAsync(warning, ct);
            if (warning.Ordinal == 3)
            {
                session.Terminate(clock.UtcNow, command.AuthoritativeRevisionId);
                var final = new Submission(SubmissionId.New(), session.Id, command.ProblemId,
                    command.AuthoritativeRevisionId, command.Language, clock.UtcNow, true);
                var job = new JudgeJob(JudgeJobId.New(), final.Id, JudgeJobState.Pending, 0, clock.UtcNow);
                await store.AddSubmissionAsync(final, ct);
                await store.AddJudgeJobAsync(job, ct);
                await store.AddTerminationMetadataAsync(new(session.Id, warning.Id, command.AuthoritativeRevisionId, final.Id, job.Id, clock.UtcNow), ct);
                await judgeQueue.EnqueueAsync(job, ct);
                await outbox.AddAsync(new SessionTerminated(session.Id, warning.Id, command.AuthoritativeRevisionId,
                    final.Id, job.Id, session.TerminatedAtUtc!.Value), ct);
                await outbox.AddAsync(new WarningIssued(session.Id, warning.Id, warning.Ordinal), ct);
            }
            else
            {
                await outbox.AddAsync(new WarningIssued(session.Id, warning.Id, warning.Ordinal), ct);
            }
            return warning;
        }, cancellationToken);
    }

    public async Task<ConfirmedWarning> IssueStoredWarningAsync(IssueStoredWarningCommand command, CancellationToken cancellationToken)
    {
        await authorization.EnsureAsync("warning.issue", command.SessionId.Value, cancellationToken);
        return await unitOfWork.ExecuteAsync(async ct =>
        {
            await EnsureNewKeyAsync(command.IdempotencyKey, ct);
            var evaluation = await store.GetEvaluationAsync(command.EvaluationId, ct) ?? throw NotFound("evaluation");
            var session = await RequiredSessionAsync(command.SessionId, ct);
            var revisionId = session.CurrentRevisionId ?? throw new ApplicationFailure(ErrorCodes.InvalidInput, "The session has no authoritative source revision.");
            var warning = session.IssueWarning(evaluation, clock.UtcNow);
            if (warning is null) throw new ApplicationFailure(ErrorCodes.IdempotencyConflict, "This evaluation already produced a warning.");
            await store.AddWarningAsync(warning, ct);
            if (warning.Ordinal == 3)
            {
                session.Terminate(clock.UtcNow, revisionId);
                var final = new Submission(SubmissionId.New(), session.Id, command.ProblemId, revisionId, command.Language, clock.UtcNow, true);
                var job = new JudgeJob(JudgeJobId.New(), final.Id, JudgeJobState.Pending, 0, clock.UtcNow);
                await store.AddSubmissionAsync(final, ct); await store.AddJudgeJobAsync(job, ct); await judgeQueue.EnqueueAsync(job, ct);
                await store.AddTerminationMetadataAsync(new(session.Id, warning.Id, revisionId, final.Id, job.Id, clock.UtcNow), ct);
                await outbox.AddAsync(new SessionTerminated(session.Id, warning.Id, revisionId, final.Id, job.Id, session.TerminatedAtUtc!.Value), ct);
                await outbox.AddAsync(new JudgeJobQueued(job.Id, final.Id), ct);
                await outbox.AddAsync(new WarningIssued(session.Id, warning.Id, warning.Ordinal), ct);
            }
            else await outbox.AddAsync(new WarningIssued(session.Id, warning.Id, warning.Ordinal), ct);
            await store.AddIdempotencyKeyAsync(command.IdempotencyKey, ct);
            return warning;
        }, cancellationToken);
    }

    public Task<ExamSummary> GetExamSummaryAsync(ExamId id, CancellationToken cancellationToken) =>
        store.GetExamSummaryAsync(id, cancellationToken);

    public async Task<ExamSession> GetCandidateSessionAsync(SessionId id, CancellationToken cancellationToken)
    {
        await authorization.EnsureAsync("session.access", id.Value, cancellationToken);
        return await RequiredSessionAsync(id, cancellationToken);
    }

    private async Task ChangeExamAsync(ExamId id, string permission, Action<Exam, DateTimeOffset> change, CancellationToken cancellationToken)
    {
        await authorization.EnsureAsync(permission, id.Value, cancellationToken);
        await unitOfWork.ExecuteAsync(async ct => { var exam = await RequiredExamAsync(id, ct); change(exam, clock.UtcNow); return true; }, cancellationToken);
    }

    private async Task<Exam> RequiredExamAsync(ExamId id, CancellationToken ct) =>
        await store.GetExamAsync(id, ct) ?? throw NotFound("exam");

    private async Task<ExamSession> RequiredSessionAsync(SessionId id, CancellationToken ct) =>
        await store.GetSessionAsync(id, ct) ?? throw NotFound("session");

    private async Task EnsureNewKeyAsync(string key, CancellationToken ct)
    {
        ValidateKey(key);
        if (await store.HasIdempotencyKeyAsync(key, ct))
            throw new ApplicationFailure(ErrorCodes.IdempotencyConflict, "The idempotency key was already used.");
    }

    private static void ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 128)
            throw new ApplicationFailure(ErrorCodes.InvalidInput, "A valid idempotency key is required.");
    }

    private static ApplicationFailure NotFound(string resource) => new(ErrorCodes.NotFound, $"The {resource} was not found.");
}
