using Mastemis.Application;
using Mastemis.Domain;

namespace Mastemis.Server.Endpoints.Examinations;

public static class ExaminationEndpoints
{
    public static void MapExaminationEndpoints(this WebApplication app, bool requireAuthorization)
    {
        var api = app.MapGroup("/api");
        if (requireAuthorization) api.RequireAuthorization();
        api.MapPost("/exams", async (CreateExamRequest request, MastemisService service, CancellationToken ct) =>
        { var exam = await service.CreateExamAsync(new(request.Title, request.IdempotencyKey), ct); return Results.Created($"/api/exams/{exam.Id.Value}", ExamResponse.From(exam)); });
        api.MapPost("/exams/{examId:guid}/open", async (Guid examId, MastemisService service, CancellationToken ct) => { await service.OpenExamAsync(new(examId), ct); return Results.NoContent(); });
        api.MapPost("/exams/{examId:guid}/close", async (Guid examId, MastemisService service, CancellationToken ct) => { await service.CloseExamAsync(new(examId), ct); return Results.NoContent(); });
        api.MapPost("/exams/{examId:guid}/cancel", async (Guid examId, MastemisService service, CancellationToken ct) => { await service.CancelExamAsync(new(examId), ct); return Results.NoContent(); });
        api.MapPost("/exams/{examId:guid}/schedule", async (Guid examId, ScheduleExamRequest request, MastemisService service, CancellationToken ct) => { await service.ScheduleExamAsync(new(new(examId), request.StartsAtUtc, request.EndsAtUtc, request.IdempotencyKey), ct); return Results.NoContent(); });
        api.MapPost("/exams/{examId:guid}/rooms", async (Guid examId, CreateRoomRequest request, MastemisService service, CancellationToken ct) =>
        { var room = await service.CreateRoomAsync(new(new(examId), request.Name, request.IdempotencyKey), ct); return Results.Created($"/api/rooms/{room.Id.Value}", new { id = room.Id.Value, examId = room.ExamId.Value, room.Name }); });
        api.MapPost("/exams/{examId:guid}/candidates", async (Guid examId, RegisterCandidateRequest request, MastemisService service, CancellationToken ct) =>
        { var candidate = await service.RegisterCandidateAsync(new(new(examId), new(request.UserId), request.RegistrationCode, request.IdempotencyKey), ct); return Results.Created($"/api/candidates/{candidate.Id.Value}", new { id = candidate.Id.Value, userId = candidate.UserId.Value, candidate.RegistrationCode }); });
        api.MapPost("/sessions", async (StartSessionRequest request, MastemisService service, CancellationToken ct) =>
        { var session = await service.StartExamSessionAsync(new(new(request.ExamId), new(request.RoomId), new(request.CandidateId), request.IdempotencyKey), ct); return Results.Created($"/api/sessions/{session.Id.Value}", SessionResponse.From(session)); });
        api.MapGet("/sessions/{sessionId:guid}", async (Guid sessionId, MastemisService service, CancellationToken ct) => SessionResponse.From(await service.GetCandidateSessionAsync(new(sessionId), ct)));
        api.MapPost("/sessions/{sessionId:guid}/drafts", SaveDraftAsync).DisableAntiforgery();
        api.MapPost("/sessions/{sessionId:guid}/submissions", async (Guid sessionId, CreateSubmissionRequest request, MastemisService service, CancellationToken ct) =>
        { var submission = await service.CreateSubmissionAsync(new(new(sessionId), new(request.ProblemId), new(request.RevisionId), request.Language, request.IdempotencyKey), ct); return Results.Created($"/api/submissions/{submission.Id.Value}", SubmissionResponse.From(submission)); });
        api.MapGet("/submissions/{submissionId:guid}", async (Guid submissionId, MastemisService service, CancellationToken ct) => SubmissionResponse.From(await service.GetSubmissionAsync(new(submissionId), ct)));
        api.MapGet("/sessions/{sessionId:guid}/submissions", async (Guid sessionId, MastemisService service, CancellationToken ct) => (await service.GetSubmissionHistoryAsync(new(sessionId), ct)).Select(SubmissionResponse.From));
        api.MapPost("/sessions/{sessionId:guid}/sfe-events", async (Guid sessionId, SfeEventRequest request, MastemisService service, CancellationToken ct) =>
        { var value = await service.RecordRawSfeEventAsync(new(new(sessionId), request.ClientSequence, request.ClientTimestamp, request.EventType, request.Metadata ?? new Dictionary<string, string>(), request.IdempotencyKey), ct); return Results.Accepted($"/api/sfe-events/{value.Id.Value}", new { id = value.Id.Value, value.ServerReceivedAtUtc }); });
        api.MapPost("/sfe-events/{eventId:guid}/evaluate", async (Guid eventId, EvaluateSfeRequest request, MastemisService service, CancellationToken ct) => await service.EvaluateSfeEventAsync(new(new(eventId), request.DurationMilliseconds is { } duration ? TimeSpan.FromMilliseconds(duration) : null, request.ConcurrentSession), ct));
        api.MapPost("/sessions/{sessionId:guid}/warnings", async (Guid sessionId, IssueWarningRequest request, MastemisService service, CancellationToken ct) => await service.IssueStoredWarningAsync(new(new(sessionId), new(request.EvaluationId), new(request.ProblemId), request.Language, request.IdempotencyKey), ct));
        api.MapGet("/exams/{examId:guid}/summary", async (Guid examId, MastemisService service, CancellationToken ct) => await service.GetExamSummaryAsync(new(examId), ct));
    }

    private static async Task<IResult> SaveDraftAsync(Guid sessionId, SaveDraftRequest request, MastemisService service, CancellationToken ct)
    {
        byte[] content;
        try { content = Convert.FromBase64String(request.ContentBase64); }
        catch (FormatException) { throw new ApplicationFailure(ErrorCodes.InvalidInput, "Source content must be base64 encoded."); }
        var revision = await service.SaveDraftRevisionAsync(new(new(sessionId), content, request.IdempotencyKey), ct);
        return Results.Created($"/api/sessions/{sessionId}/drafts/{revision.Id.Value}", new { id = revision.Id.Value, revision.Sha256, revision.CreatedAtUtc });
    }
}

public sealed record CreateExamRequest(string Title, string IdempotencyKey);
public sealed record ScheduleExamRequest(DateTimeOffset StartsAtUtc, DateTimeOffset EndsAtUtc, string IdempotencyKey);
public sealed record CreateRoomRequest(string Name, string IdempotencyKey);
public sealed record RegisterCandidateRequest(Guid UserId, string RegistrationCode, string IdempotencyKey);
public sealed record StartSessionRequest(Guid ExamId, Guid RoomId, Guid CandidateId, string IdempotencyKey);
public sealed record SaveDraftRequest(string ContentBase64, string IdempotencyKey);
public sealed record CreateSubmissionRequest(Guid ProblemId, Guid RevisionId, string Language, string IdempotencyKey);
public sealed record SfeEventRequest(long ClientSequence, DateTimeOffset ClientTimestamp, string EventType, Dictionary<string, string>? Metadata, string IdempotencyKey);
public sealed record EvaluateSfeRequest(double? DurationMilliseconds, bool ConcurrentSession);
public sealed record IssueWarningRequest(Guid EvaluationId, Guid ProblemId, string Language, string IdempotencyKey);
public sealed record ExamResponse(Guid Id, string Title, string State) { public static ExamResponse From(Exam exam) => new(exam.Id.Value, exam.Title, exam.State.ToString()); }
public sealed record SessionResponse(Guid Id, Guid ExamId, Guid RoomId, Guid CandidateId, string State, int WarningCount, Guid? FrozenRevisionId)
{ public static SessionResponse From(ExamSession session) => new(session.Id.Value, session.ExamId.Value, session.RoomId.Value, session.CandidateId.Value, session.State.ToString(), session.Warnings.Count, session.FrozenRevisionId?.Value); }
public sealed record SubmissionResponse(Guid Id, Guid SessionId, Guid ProblemId, Guid RevisionId, string Language, string State, bool IsFinal)
{ public static SubmissionResponse From(Submission submission) => new(submission.Id.Value, submission.SessionId.Value, submission.ProblemId.Value, submission.RevisionId.Value, submission.Language, submission.State.ToString(), submission.IsFinal); }
