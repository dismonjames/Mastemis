using System.Text.Json;
using Mastemis.Application;
using Mastemis.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Mastemis.Infrastructure.Persistence;

public sealed class PostgresRuntime(MastemisDbContext db, IClock clock)
    : IAggregateStore, IUnitOfWork, ITransactionalOutbox
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
        var row = PersistenceMapper.ToRow(exam);
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
        var row = PersistenceMapper.ToRow(session);
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
        db.Submissions.Add(PersistenceMapper.ToRow(submission));
        return Task.CompletedTask;
    }

    public async Task<Submission?> GetSubmissionAsync(SubmissionId id, CancellationToken cancellationToken)
    {
        var row = await db.Submissions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id.Value, cancellationToken);
        return row is null ? null : PersistenceMapper.ToDomain(row);
    }

    public async Task<IReadOnlyList<Submission>> GetSubmissionsAsync(SessionId sessionId, CancellationToken cancellationToken) =>
        (await db.Submissions.AsNoTracking().Where(x => x.SessionId == sessionId.Value)
            .OrderByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken)).Select(PersistenceMapper.ToDomain).ToArray();

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
        if (!db.JudgeJobs.Local.Any(x => x.Id == job.Id.Value)) db.JudgeJobs.Add(PersistenceMapper.ToRow(job));
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

    public Task AddAsync<T>(T message, CancellationToken cancellationToken) where T : notnull
    {
        cancellationToken.ThrowIfCancellationRequested();
        db.OutboxMessages.Add(new OutboxRow
        {
            Id = Guid.NewGuid(),
            Type = typeof(T).FullName ?? typeof(T).Name,
            Payload = JsonSerializer.Serialize(message),
            ResourceId = PersistenceMapper.GetResourceId(message),
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
            var source = PersistenceMapper.ToRow(pair.Domain); pair.Row.Title = source.Title; pair.Row.State = source.State;
            pair.Row.StartsAtUtc = source.StartsAtUtc; pair.Row.EndsAtUtc = source.EndsAtUtc;
        }
        foreach (var pair in _trackedSessions.Values)
        {
            var source = PersistenceMapper.ToRow(pair.Domain); pair.Row.State = source.State; pair.Row.StartedAtUtc = source.StartedAtUtc;
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

}
