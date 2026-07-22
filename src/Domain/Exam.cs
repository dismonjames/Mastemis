namespace Mastemis.Domain;

public sealed class Exam
{
    private Exam() { }

    public Exam(ExamId id, string title, DateTimeOffset createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new DomainException("exam.title_required", "An exam title is required.");
        Id = id;
        Title = title.Trim();
        CreatedAtUtc = EnsureUtc(createdAtUtc);
    }

    public ExamId Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public ExamState State { get; private set; } = ExamState.Draft;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? StartsAtUtc { get; private set; }
    public DateTimeOffset? EndsAtUtc { get; private set; }

    public static Exam Restore(ExamId id, string title, ExamState state, DateTimeOffset createdAtUtc,
        DateTimeOffset? startsAtUtc, DateTimeOffset? endsAtUtc)
    {
        var exam = new Exam(id, title, createdAtUtc)
        {
            State = state,
            StartsAtUtc = startsAtUtc?.ToUniversalTime(),
            EndsAtUtc = endsAtUtc?.ToUniversalTime()
        };
        return exam;
    }

    public void Schedule(DateTimeOffset startsAtUtc, DateTimeOffset endsAtUtc)
    {
        Require(ExamState.Draft);
        startsAtUtc = EnsureUtc(startsAtUtc);
        endsAtUtc = EnsureUtc(endsAtUtc);
        if (endsAtUtc <= startsAtUtc) throw new DomainException("exam.invalid_schedule", "Exam end must follow its start.");
        StartsAtUtc = startsAtUtc;
        EndsAtUtc = endsAtUtc;
        State = ExamState.Scheduled;
    }

    public void Open(DateTimeOffset nowUtc)
    {
        _ = EnsureUtc(nowUtc);
        if (State is not (ExamState.Draft or ExamState.Scheduled)) InvalidTransition(ExamState.Open);
        State = ExamState.Open;
    }

    public void Close(DateTimeOffset nowUtc)
    {
        _ = EnsureUtc(nowUtc);
        Require(ExamState.Open);
        State = ExamState.Closed;
    }

    public void Cancel(DateTimeOffset nowUtc)
    {
        _ = EnsureUtc(nowUtc);
        if (State is ExamState.Open or ExamState.Closed or ExamState.Cancelled) InvalidTransition(ExamState.Cancelled);
        State = ExamState.Cancelled;
    }

    private void Require(ExamState expected)
    {
        if (State != expected) InvalidTransition(expected);
    }

    private void InvalidTransition(ExamState target) =>
        throw new DomainException("exam.invalid_transition", $"Cannot transition exam from {State} to {target}.");

    internal static DateTimeOffset EnsureUtc(DateTimeOffset value) => value.ToUniversalTime();
}
