using Mastemis.Application;
using Mastemis.Domain;

namespace Mastemis.Infrastructure.Persistence;

internal static class PersistenceMapper
{
    public static ExamRow ToRow(Exam value) => new()
    {
        Id = value.Id.Value,
        Title = value.Title,
        State = (int)value.State,
        CreatedAtUtc = value.CreatedAtUtc,
        StartsAtUtc = value.StartsAtUtc,
        EndsAtUtc = value.EndsAtUtc
    };
    public static SessionRow ToRow(ExamSession value) => new()
    {
        Id = value.Id.Value,
        ExamId = value.ExamId.Value,
        RoomId = value.RoomId.Value,
        CandidateId = value.CandidateId.Value,
        State = (int)value.State,
        StartedAtUtc = value.StartedAtUtc,
        TerminatedAtUtc = value.TerminatedAtUtc,
        CurrentRevisionId = value.CurrentRevisionId?.Value,
        FrozenRevisionId = value.FrozenRevisionId?.Value,
        Version = value.Version,
        ConcurrencyToken = Guid.NewGuid()
    };
    public static SubmissionRow ToRow(Submission value) => new()
    {
        Id = value.Id.Value,
        SessionId = value.SessionId.Value,
        ProblemId = value.ProblemId.Value,
        RevisionId = value.RevisionId.Value,
        Language = value.Language,
        State = (int)value.State,
        IsFinal = value.IsFinal,
        CreatedAtUtc = value.CreatedAtUtc
    };
    public static Submission ToDomain(SubmissionRow row) => new(new(row.Id), new(row.SessionId), new(row.ProblemId),
        new(row.RevisionId), row.Language, row.CreatedAtUtc, row.IsFinal)
    { State = (SubmissionState)row.State };
    public static JudgeJobRow ToRow(JudgeJob value) => new()
    {
        Id = value.Id.Value,
        SubmissionId = value.SubmissionId.Value,
        State = (int)value.State,
        Attempt = value.Attempt,
        CreatedAtUtc = value.CreatedAtUtc,
        AvailableAtUtc = value.CreatedAtUtc,
        LeaseExpiresAtUtc = value.LeaseExpiresAtUtc,
        WorkerId = value.WorkerId?.Value,
        ConcurrencyToken = Guid.NewGuid()
    };
    public static JudgeJob ToDomain(JudgeJobRow row) => new(new(row.Id), new(row.SubmissionId),
        (JudgeJobState)row.State, row.Attempt, row.CreatedAtUtc, row.LeaseExpiresAtUtc,
        row.WorkerId is { } worker ? new(worker) : null);
    public static string? GetResourceId<T>(T message) => message switch
    {
        SessionTerminated value => value.SessionId.Value.ToString("D"),
        WarningIssued value => value.SessionId.Value.ToString("D"),
        DraftSaved value => value.SessionId.Value.ToString("D"),
        SubmissionCreated value => value.SessionId.Value.ToString("D"),
        _ => null
    };
}
