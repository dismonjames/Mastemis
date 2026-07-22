using Mastemis.Domain;

namespace Mastemis.Domain.Tests;

public sealed class ExamSessionTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Session_cannot_start_before_exam_is_open()
    {
        var exam = NewExam();
        var session = NewSession(exam);
        var error = Assert.Throws<DomainException>(() => session.Start(exam, Now));
        Assert.Equal("session.exam_not_open", error.Code);
    }

    [Fact]
    public void Raw_event_does_not_increment_warning_count()
    {
        var (session, _) = StartedSession();
        _ = new ViolationEvent(ViolationEventId.New(), session.Id, 1, Now, Now, "PageHidden", new Dictionary<string, string>());
        Assert.Empty(session.Warnings);
    }

    [Theory]
    [InlineData(EvaluationResult.Ignored)]
    [InlineData(EvaluationResult.Recorded)]
    [InlineData(EvaluationResult.Suspected)]
    public void Only_confirmed_evaluations_create_warnings(EvaluationResult result)
    {
        var (session, _) = StartedSession();
        var evaluation = Evaluation(session, result);
        var error = Assert.Throws<DomainException>(() => session.IssueWarning(evaluation, Now));
        Assert.Equal("warning.not_confirmed", error.Code);
        Assert.Empty(session.Warnings);
    }

    [Fact]
    public void Duplicate_evaluation_is_idempotent()
    {
        var (session, _) = StartedSession();
        var evaluation = Evaluation(session, EvaluationResult.ConfirmedViolation);
        Assert.NotNull(session.IssueWarning(evaluation, Now));
        Assert.Null(session.IssueWarning(evaluation, Now.AddSeconds(1)));
        Assert.Single(session.Warnings);
    }

    [Fact]
    public void First_two_warnings_keep_session_active_and_third_allows_termination()
    {
        var (session, _) = StartedSession();
        for (var index = 0; index < 3; index++)
        {
            session.IssueWarning(Evaluation(session, EvaluationResult.ConfirmedViolation), Now.AddSeconds(index));
            Assert.Equal(SessionState.Active, session.State);
        }
        var revision = SourceRevisionId.New();
        session.Terminate(Now.AddMinutes(1), revision);
        Assert.Equal(SessionState.Terminated, session.State);
        Assert.Equal(revision, session.FrozenRevisionId);
        Assert.Equal(Now.AddMinutes(1), session.TerminatedAtUtc);
    }

    [Fact]
    public void Terminated_session_rejects_writes_and_submissions()
    {
        var (session, _) = StartedSession();
        for (var index = 0; index < 3; index++) session.IssueWarning(Evaluation(session, EvaluationResult.ConfirmedViolation), Now);
        session.Terminate(Now, SourceRevisionId.New());
        Assert.Equal("session.draft_rejected", Assert.Throws<DomainException>(() => session.SaveRevision(SourceRevisionId.New())).Code);
        Assert.Equal("session.submission_rejected", Assert.Throws<DomainException>(session.EnsureMaySubmit).Code);
    }

    [Fact]
    public void Completed_session_rejects_writes()
    {
        var (session, _) = StartedSession();
        session.Complete();
        Assert.Equal("session.draft_rejected", Assert.Throws<DomainException>(() => session.SaveRevision(SourceRevisionId.New())).Code);
    }

    [Fact]
    public void Exam_rejects_invalid_transitions_and_schedule()
    {
        var exam = NewExam();
        Assert.Equal("exam.invalid_schedule", Assert.Throws<DomainException>(() => exam.Schedule(Now, Now)).Code);
        exam.Open(Now);
        Assert.Equal("exam.invalid_transition", Assert.Throws<DomainException>(() => exam.Cancel(Now)).Code);
    }

    private static Exam NewExam() => new(ExamId.New(), "Final", Now);
    private static ExamSession NewSession(Exam exam) => new(SessionId.New(), exam.Id, RoomId.New(), CandidateId.New());
    private static (ExamSession Session, Exam Exam) StartedSession()
    {
        var exam = NewExam();
        exam.Open(Now);
        var session = NewSession(exam);
        session.Start(exam, Now);
        return (session, exam);
    }
    private static ViolationEvaluation Evaluation(ExamSession session, EvaluationResult result) =>
        new(ViolationEvaluationId.New(), ViolationEventId.New(), session.Id, result, "test", Now);
}
