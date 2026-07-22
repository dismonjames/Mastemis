using System.Text.Json;
using Mastemis.Application;
using Mastemis.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Mastemis.Infrastructure.Persistence;

public sealed class PostgresRuntime(MastemisDbContext db, IClock clock)
    : IAggregateStore, IUnitOfWork, IDurableJudgeQueue, ITransactionalOutbox
{
    private readonly Dictionary<ExamId, (Exam Domain, ExamRow Row)> _trackedExams = [];
    private readonly Dictionary<SessionId, (ExamSession Domain, SessionRow Row)> _trackedSessions = [];

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await operation(cancellationToken);
            await SynchronizeTrackedRowsAsync(cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new ApplicationFailure(ErrorCodes.ConcurrencyConflict, "The resource changed concurrently.");
        }
        catch (DbUpdateException exception) when (exception.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new ApplicationFailure(ErrorCodes.IdempotencyConflict, "A unique operation or resource was already persisted.");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            _trackedExams.Clear();
            _trackedSessions.Clear();
            db.ChangeTracker.Clear();
        }
    }

    public Task AddExamAsync(Exam exam, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var row = ToRow(exam);
        db.Exams.Add(row);
        _trackedExams[exam.Id] = (exam, row);
        return Task.CompletedTask;
    }

    public async Task<Exam?> GetExamAsync(ExamId id, CancellationToken cancellationToken)
    {
        if (_trackedExams.TryGetValue(id, out var existing)) return existing.Domain;
        var row = await db.Exams.SingleOrDefaultAsync(x => x.Id == id.Value, cancellationToken);
        if (row is null) return null;
        var domain = Exam.Restore(id, row.Title, (ExamState)row.State, row.CreatedAtUtc, row.StartsAtUtc, row.EndsAtUtc);
        _trackedExams[id] = (domain, row);
        return domain;
    }

    public Task AddRoomAsync(ExamRoom room, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        db.Rooms.Add(new RoomRow { Id = room.Id.Value, ExamId = room.ExamId.Value, Code = room.Name, Name = room.Name });
        return Task.CompletedTask;
    }

    public async Task<ExamRoom?> GetRoomAsync(RoomId id, CancellationToken cancellationToken)
    {
        var row = await db.Rooms.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id.Value, cancellationToken);
        return row is null ? null : new ExamRoom(id, new ExamId(row.ExamId), row.Name);
    }

    public async Task AddCandidateAsync(Candidate candidate, ExamId examId, CancellationToken cancellationToken)
    {
        db.Candidates.Add(new CandidateRow { Id = candidate.Id.Value, UserId = candidate.UserId.Value });
        db.CandidateRegistrations.Add(new CandidateRegistrationRow
        {
            Id = Guid.NewGuid(),
            ExamId = examId.Value,
            CandidateId = candidate.Id.Value,
            RegistrationCode = candidate.RegistrationCode,
            AccessState = (int)CandidateExamAccessState.Enabled,
            CreatedAtUtc = clock.UtcNow
        });
        await Task.CompletedTask;
    }

    public Task AddSessionAsync(ExamSession session, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var row = ToRow(session);
        db.ExamSessions.Add(row);
        _trackedSessions[session.Id] = (session, row);
        return Task.CompletedTask;
    }

    public async Task<ExamSession?> GetSessionAsync(SessionId id, CancellationToken cancellationToken)
    {
        if (_trackedSessions.TryGetValue(id, out var existing)) return existing.Domain;
        var row = await db.ExamSessions.SingleOrDefaultAsync(x => x.Id == id.Value, cancellationToken);
        if (row is null) return null;
        var warningRows = await db.ConfirmedWarnings.AsNoTracking().Where(x => x.SessionId == row.Id)
            .OrderBy(x => x.Ordinal).ToListAsync(cancellationToken);
        var warnings = warningRows.Select(x => new ConfirmedWarning(new WarningId(x.Id), id,
            new ViolationEvaluationId(x.EvaluationId), x.Ordinal, x.IssuedAtUtc));
        var domain = ExamSession.Restore(id, new ExamId(row.ExamId), new RoomId(row.RoomId), new CandidateId(row.CandidateId),
            (SessionState)row.State, row.StartedAtUtc, row.TerminatedAtUtc,
            row.CurrentRevisionId is { } current ? new SourceRevisionId(current) : null,
            row.FrozenRevisionId is { } frozen ? new SourceRevisionId(frozen) : null, row.Version, warnings);
        _trackedSessions[id] = (domain, row);
        return domain;
    }

    public Task AddRevisionAsync(SourceRevisionMetadata revision, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        db.SourceRevisions.Add(new SourceRevisionRow
        {
            Id = revision.Id.Value,
            SessionId = revision.SessionId.Value,
            ObjectId = revision.ObjectId,
            Sha256 = revision.Sha256,
            Length = revision.Length,
            CreatedAtUtc = revision.CreatedAtUtc
        });
        return Task.CompletedTask;
    }

    public Task AddSubmissionAsync(Submission submission, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        db.Submissions.Add(ToRow(submission));
        return Task.CompletedTask;
    }

    public async Task<Submission?> GetSubmissionAsync(SubmissionId id, CancellationToken cancellationToken)
    {
        var row = await db.Submissions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id.Value, cancellationToken);
        return row is null ? null : ToDomain(row);
    }

    public async Task<IReadOnlyList<Submission>> GetSubmissionsAsync(SessionId sessionId, CancellationToken cancellationToken) =>
        (await db.Submissions.AsNoTracking().Where(x => x.SessionId == sessionId.Value)
            .OrderByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken)).Select(ToDomain).ToArray();

    public Task AddEventAsync(ViolationEvent activityEvent, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        db.SfeEvents.Add(new SfeEventRow
        {
            Id = activityEvent.Id.Value,
            SessionId = activityEvent.SessionId.Value,
            ClientSequence = activityEvent.ClientSequence,
            ClientTimestamp = activityEvent.ClientTimestamp,
            ServerReceivedAtUtc = activityEvent.ServerReceivedAtUtc,
            EventType = activityEvent.EventType,
            MetadataJson = JsonSerializer.Serialize(activityEvent.Metadata)
        });
        return Task.CompletedTask;
    }

    public async Task<ViolationEvent?> GetEventAsync(ViolationEventId id, CancellationToken cancellationToken)
    {
        var row = await db.SfeEvents.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id.Value, cancellationToken);
        return row is null ? null : new ViolationEvent(id, new SessionId(row.SessionId), row.ClientSequence,
            row.ClientTimestamp, row.ServerReceivedAtUtc, row.EventType,
            JsonSerializer.Deserialize<Dictionary<string, string>>(row.MetadataJson) ?? []);
    }

    public Task AddEvaluationAsync(ViolationEvaluation evaluation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        db.SfeEvaluations.Add(new SfeEvaluationRow
        {
            Id = evaluation.Id.Value,
            EventId = evaluation.EventId.Value,
            SessionId = evaluation.SessionId.Value,
            Result = (int)evaluation.Result,
            ReasonCode = evaluation.PolicyCode,
            PolicyVersion = "baseline.v1",
            EvaluatedAtUtc = evaluation.EvaluatedAtUtc
        });
        return Task.CompletedTask;
    }

    public async Task<ViolationEvaluation?> GetEvaluationAsync(ViolationEvaluationId id, CancellationToken cancellationToken)
    {
        var row = await db.SfeEvaluations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id.Value, cancellationToken);
        return row is null ? null : new ViolationEvaluation(id, new ViolationEventId(row.EventId), new SessionId(row.SessionId),
            (EvaluationResult)row.Result, row.ReasonCode, row.EvaluatedAtUtc);
    }

    public async Task AddWarningAsync(ConfirmedWarning warning, CancellationToken cancellationToken)
    {
        var session = await db.ExamSessions.SingleAsync(x => x.Id == warning.SessionId.Value, cancellationToken);
        db.ConfirmedWarnings.Add(new WarningRow
        {
            Id = warning.Id.Value,
            ExamId = session.ExamId,
            RoomId = session.RoomId,
            CandidateId = session.CandidateId,
            SessionId = warning.SessionId.Value,
            EvaluationId = warning.EvaluationId.Value,
            Ordinal = warning.Ordinal,
            IssuedAtUtc = warning.IssuedAtUtc
        });
    }

    public Task AddJudgeJobAsync(JudgeJob job, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!db.JudgeJobs.Local.Any(x => x.Id == job.Id.Value)) db.JudgeJobs.Add(ToRow(job));
        return Task.CompletedTask;
    }

    public Task AddTerminationMetadataAsync(TerminationMetadata metadata, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        db.TerminationMetadata.Add(new TerminationMetadataRow
        {
            SessionId = metadata.SessionId.Value,
            WarningId = metadata.WarningId.Value,
            FrozenRevisionId = metadata.FrozenRevisionId.Value,
            FinalSubmissionId = metadata.FinalSubmissionId.Value,
            JudgeJobId = metadata.JudgeJobId.Value,
            CreatedAtUtc = metadata.CreatedAtUtc
        });
        return Task.CompletedTask;
    }

    public Task<bool> HasIdempotencyKeyAsync(string key, CancellationToken cancellationToken) =>
        db.IdempotencyRecords.AnyAsync(x => x.Operation == "application" && x.Caller == "current" && x.Key == key, cancellationToken);

    public Task AddIdempotencyKeyAsync(string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        db.IdempotencyRecords.Add(new IdempotencyRow { Operation = "application", Caller = "current", Key = key, CreatedAtUtc = clock.UtcNow });
        return Task.CompletedTask;
    }

    public async Task<ExamSummary> GetExamSummaryAsync(ExamId examId, CancellationToken cancellationToken)
    {
        var sessions = db.ExamSessions.AsNoTracking().Where(x => x.ExamId == examId.Value);
        var active = await sessions.CountAsync(x => x.State == (int)SessionState.Active, cancellationToken);
        var disconnected = await sessions.CountAsync(x => x.State == (int)SessionState.Disconnected, cancellationToken);
        var terminated = await sessions.CountAsync(x => x.State == (int)SessionState.Terminated, cancellationToken);
        var warnings = await db.ConfirmedWarnings.CountAsync(x => x.ExamId == examId.Value, cancellationToken);
        var queued = await (from submission in db.Submissions
                            join job in db.JudgeJobs on submission.Id equals job.SubmissionId
                            join session in db.ExamSessions on submission.SessionId equals session.Id
                            where session.ExamId == examId.Value && job.State == (int)JudgeJobState.Pending
                            select job).CountAsync(cancellationToken);
        return new ExamSummary(examId, active, disconnected, warnings, terminated, queued);
    }

    public Task EnqueueAsync(JudgeJob job, CancellationToken cancellationToken) => AddJudgeJobAsync(job, cancellationToken);

    public async Task<JudgeJob?> ClaimAsync(JudgeWorkerId workerId, TimeSpan lease, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var now = clock.UtcNow;
        var row = await db.JudgeJobs.FromSqlInterpolated($$"""
            SELECT * FROM judge_jobs
            WHERE (("State" = {{(int)JudgeJobState.Pending}} AND "AvailableAtUtc" <= {{now}})
                OR ("State" = {{(int)JudgeJobState.Claimed}} AND "LeaseExpiresAtUtc" <= {{now}}))
              AND "Attempt" < "MaximumAttempts"
            ORDER BY "Priority" DESC, "CreatedAtUtc"
            FOR UPDATE SKIP LOCKED
            LIMIT 1
            """).SingleOrDefaultAsync(cancellationToken);
        if (row is null) { await transaction.CommitAsync(cancellationToken); return null; }
        row.State = (int)JudgeJobState.Claimed; row.WorkerId = workerId.Value; row.LeaseId = Guid.NewGuid();
        row.LeaseExpiresAtUtc = now + lease; row.Attempt++; row.ConcurrencyToken = Guid.NewGuid();
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToDomain(row);
    }

    public async Task CompleteAsync(JudgeJobId jobId, Judgement judgement, CancellationToken cancellationToken)
    {
        var row = await db.JudgeJobs.SingleOrDefaultAsync(x => x.Id == jobId.Value, cancellationToken)
            ?? throw new ApplicationFailure(ErrorCodes.NotFound, "Judge job not found.");
        if (row.SubmissionId != judgement.SubmissionId.Value)
            throw new ApplicationFailure(ErrorCodes.LeaseRejected, "The judgement does not match this job.");
        if (row.State == (int)JudgeJobState.Completed) return;
        row.State = (int)JudgeJobState.Completed; row.CompletedAtUtc = judgement.CompletedAtUtc;
        row.LeaseExpiresAtUtc = null; row.ConcurrencyToken = Guid.NewGuid();
        db.Judgements.Add(new JudgementRow
        {
            SubmissionId = judgement.SubmissionId.Value,
            Verdict = (int)judgement.Verdict,
            Score = judgement.Score,
            CompletedAtUtc = judgement.CompletedAtUtc
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task AddAsync<T>(T message, CancellationToken cancellationToken) where T : notnull
    {
        cancellationToken.ThrowIfCancellationRequested();
        db.OutboxMessages.Add(new OutboxRow
        {
            Id = Guid.NewGuid(),
            Type = typeof(T).FullName ?? typeof(T).Name,
            Payload = JsonSerializer.Serialize(message),
            ResourceId = GetResourceId(message),
            OccurredAtUtc = clock.UtcNow,
            CreatedAtUtc = clock.UtcNow,
            NextAttemptAtUtc = clock.UtcNow
        });
        return Task.CompletedTask;
    }

    private async Task SynchronizeTrackedRowsAsync(CancellationToken cancellationToken)
    {
        foreach (var pair in _trackedExams.Values)
        {
            var source = ToRow(pair.Domain); pair.Row.Title = source.Title; pair.Row.State = source.State;
            pair.Row.StartsAtUtc = source.StartsAtUtc; pair.Row.EndsAtUtc = source.EndsAtUtc;
        }
        foreach (var pair in _trackedSessions.Values)
        {
            var source = ToRow(pair.Domain); pair.Row.State = source.State; pair.Row.StartedAtUtc = source.StartedAtUtc;
            pair.Row.TerminatedAtUtc = source.TerminatedAtUtc; pair.Row.CurrentRevisionId = source.CurrentRevisionId;
            pair.Row.FrozenRevisionId = source.FrozenRevisionId; pair.Row.Version = source.Version;
            pair.Row.ConcurrencyToken = Guid.NewGuid();
            if (pair.Domain.State == SessionState.Terminated)
            {
                var registration = await db.CandidateRegistrations.SingleAsync(x => x.ExamId == pair.Domain.ExamId.Value && x.CandidateId == pair.Domain.CandidateId.Value, cancellationToken);
                registration.AccessState = (int)CandidateExamAccessState.Terminated;
                db.AuditRecords.Add(new AuditRow
                {
                    Id = Guid.NewGuid(),
                    Action = "session.terminated",
                    ResourceType = "session",
                    ResourceId = pair.Domain.Id.Value.ToString("D"),
                    OccurredAtUtc = pair.Domain.TerminatedAtUtc!.Value
                });
            }
        }
    }

    private static ExamRow ToRow(Exam exam) => new()
    {
        Id = exam.Id.Value,
        Title = exam.Title,
        State = (int)exam.State,
        CreatedAtUtc = exam.CreatedAtUtc,
        StartsAtUtc = exam.StartsAtUtc,
        EndsAtUtc = exam.EndsAtUtc
    };
    private static SessionRow ToRow(ExamSession session) => new()
    {
        Id = session.Id.Value,
        ExamId = session.ExamId.Value,
        RoomId = session.RoomId.Value,
        CandidateId = session.CandidateId.Value,
        State = (int)session.State,
        StartedAtUtc = session.StartedAtUtc,
        TerminatedAtUtc = session.TerminatedAtUtc,
        CurrentRevisionId = session.CurrentRevisionId?.Value,
        FrozenRevisionId = session.FrozenRevisionId?.Value,
        Version = session.Version,
        ConcurrencyToken = Guid.NewGuid()
    };
    private static SubmissionRow ToRow(Submission submission) => new()
    {
        Id = submission.Id.Value,
        SessionId = submission.SessionId.Value,
        ProblemId = submission.ProblemId.Value,
        RevisionId = submission.RevisionId.Value,
        Language = submission.Language,
        State = (int)submission.State,
        IsFinal = submission.IsFinal,
        CreatedAtUtc = submission.CreatedAtUtc
    };
    private static Submission ToDomain(SubmissionRow row) => new(new SubmissionId(row.Id), new SessionId(row.SessionId),
        new ProblemId(row.ProblemId), new SourceRevisionId(row.RevisionId), row.Language, row.CreatedAtUtc, row.IsFinal)
    { State = (SubmissionState)row.State };
    private static JudgeJobRow ToRow(JudgeJob job) => new()
    {
        Id = job.Id.Value,
        SubmissionId = job.SubmissionId.Value,
        State = (int)job.State,
        Attempt = job.Attempt,
        CreatedAtUtc = job.CreatedAtUtc,
        AvailableAtUtc = job.CreatedAtUtc,
        LeaseExpiresAtUtc = job.LeaseExpiresAtUtc,
        WorkerId = job.WorkerId?.Value,
        ConcurrencyToken = Guid.NewGuid()
    };
    private static JudgeJob ToDomain(JudgeJobRow row) => new(new JudgeJobId(row.Id), new SubmissionId(row.SubmissionId),
        (JudgeJobState)row.State, row.Attempt, row.CreatedAtUtc, row.LeaseExpiresAtUtc,
        row.WorkerId is { } worker ? new JudgeWorkerId(worker) : null);
    private static string? GetResourceId<T>(T message) => message switch
    {
        SessionTerminated value => value.SessionId.Value.ToString("D"),
        WarningIssued value => value.SessionId.Value.ToString("D"),
        DraftSaved value => value.SessionId.Value.ToString("D"),
        SubmissionCreated value => value.SessionId.Value.ToString("D"),
        _ => null
    };
}
