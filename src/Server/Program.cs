using System.Diagnostics;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Mastemis.Application;
using Mastemis.Domain;
using Mastemis.Infrastructure;
using Mastemis.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddJsonConsole();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddSignalR();
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");
builder.Services.AddHttpContextAccessor();
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live", "ready"])
    .AddCheck<PostgresHealthCheck>("postgresql", tags: ["ready"])
    .AddCheck<StorageHealthCheck>("storage", tags: ["ready"]);
builder.Services.AddRateLimiter(options => options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ =>
        new FixedWindowRateLimiterOptions { PermitLimit = 200, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })));
var connectionString = builder.Configuration.GetConnectionString("Mastemis");
var durableMode = !string.IsNullOrWhiteSpace(connectionString);
if (durableMode)
{
    builder.Services.AddDbContext<MastemisDbContext>(options => options.UseNpgsql(connectionString));
    builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.Password.RequiredLength = 12; options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true; options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true; options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15); options.User.RequireUniqueEmail = false;
    }).AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<MastemisDbContext>().AddSignInManager();
    var authentication = builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
        options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
    });
    authentication.AddIdentityCookies();
    authentication.AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions,
        WorkerAuthenticationHandler>(WorkerAuthenticationDefaults.Scheme, _ => { });
    var configuredMinutes = int.TryParse(builder.Configuration["Identity:SessionMinutes"], out var parsedMinutes) ? parsedMinutes : 60;
    var sessionMinutes = Math.Clamp(configuredMinutes, 5, 720);
    builder.Services.Configure<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, options =>
    {
        options.Cookie.Name = "Mastemis.Session"; options.Cookie.HttpOnly = true; options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict; options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(sessionMinutes); options.LoginPath = "/api/auth/login";
        options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
        options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
    });
    builder.Services.AddScoped<PostgresRuntime>();
    builder.Services.AddScoped<IAggregateStore>(sp => sp.GetRequiredService<PostgresRuntime>());
    builder.Services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<PostgresRuntime>());
    builder.Services.AddScoped<IDurableJudgeQueue>(sp => sp.GetRequiredService<PostgresRuntime>());
    builder.Services.AddScoped<ITransactionalOutbox>(sp => sp.GetRequiredService<PostgresRuntime>());
    builder.Services.AddScoped<IWorkerJudgeQueue, PostgresWorkerJudgeQueue>();
    builder.Services.AddScoped<IWorkerCredentialService, WorkerCredentialService>();
    builder.Services.AddScoped<IPasswordHasher<WorkerCredentialRow>, PasswordHasher<WorkerCredentialRow>>();
    builder.Services.AddScoped<Mastemis.Application.IAuthorizationService, ProductionApplicationAuthorization>();
    builder.Services.AddSingleton<OutboxStatus>();
    builder.Services.AddSingleton<IOutboxPublisher, SignalROutboxPublisher>();
    builder.Services.AddHostedService<IdentityBootstrapService>();
    builder.Services.AddHostedService<OutboxDispatcher>();
    builder.Services.AddHealthChecks()
        .AddCheck<DatabaseSchemaHealthCheck>("database_schema", tags: ["ready"])
        .AddCheck<OutboxHealthCheck>("outbox_dispatcher", tags: ["ready"])
        .AddCheck<JudgeQueueHealthCheck>("judge_queue", tags: ["ready"]);
}
else
{
    builder.Services.AddAuthentication();
    builder.Services.AddSingleton<InMemoryRuntime>();
    builder.Services.AddSingleton<IAggregateStore>(sp => sp.GetRequiredService<InMemoryRuntime>());
    builder.Services.AddSingleton<IUnitOfWork>(sp => sp.GetRequiredService<InMemoryRuntime>());
    builder.Services.AddSingleton<IDurableJudgeQueue>(sp => sp.GetRequiredService<InMemoryRuntime>());
    builder.Services.AddSingleton<ITransactionalOutbox>(sp => sp.GetRequiredService<InMemoryRuntime>());
    if (builder.Environment.IsDevelopment()) builder.Services.AddSingleton<Mastemis.Application.IAuthorizationService, DevelopmentAuthorizationService>();
    else builder.Services.AddSingleton<Mastemis.Application.IAuthorizationService, UnconfiguredAuthorizationService>();
}
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Administrator", policy => policy.RequireRole(MastemisRoles.Administrator))
    .AddPolicy("WorkerOnly", policy => policy.AddAuthenticationSchemes(WorkerAuthenticationDefaults.Scheme).RequireRole(MastemisRoles.JudgeWorker))
    .AddPolicy("EvidenceReview", policy => policy.RequireRole(MastemisRoles.EvidenceReviewer))
    .AddPolicy("Audit", policy => policy.RequireRole(MastemisRoles.Administrator));
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<ISourceRevisionStorage>(_ => new FileSourceRevisionStorage(
    builder.Configuration["Storage:Path"] ?? Path.Combine(AppContext.BaseDirectory, "storage")));
builder.Services.AddScoped<MastemisService>();

var app = builder.Build();
app.UseExceptionHandler(exceptionApp => exceptionApp.Run(async context =>
{
    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var (status, code, title) = exception switch
    {
        ApplicationFailure failure when failure.Code == ErrorCodes.NotFound => (404, failure.Code, "Resource not found"),
        ApplicationFailure failure when failure.Code == ErrorCodes.Forbidden => (403, failure.Code, "Forbidden"),
        ApplicationFailure failure when failure.Code == ErrorCodes.IdempotencyConflict => (409, failure.Code, "Conflict"),
        ApplicationFailure failure => (400, failure.Code, "Invalid request"),
        DomainException failure => (409, failure.Code, "Domain rule rejected the operation"),
        _ => (500, "server.unexpected", "Unexpected server error")
    };
    context.Response.StatusCode = status;
    await context.Response.WriteAsJsonAsync(new ProblemDetails
    {
        Status = status,
        Title = title,
        Extensions = { ["code"] = code, ["correlationId"] = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier }
    }, context.RequestAborted);
}));
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers.ContentSecurityPolicy = "default-src 'self'; frame-ancestors 'none'";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["X-Correlation-ID"] = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
    await next(context);
});
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = check => check.Tags.Contains("live") });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.MapGet("/api/system/version", () => Results.Ok(new
{
    product = "Mastemis",
    version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0",
    telemetry = "none"
}));

if (durableMode)
{
    app.MapGet("/api/auth/antiforgery", (Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery, HttpContext context) =>
    {
        var tokens = antiforgery.GetAndStoreTokens(context);
        return Results.Ok(new { token = tokens.RequestToken });
    });
    app.MapPost("/api/auth/login", async (LoginRequest request, SignInManager<ApplicationUser> signIn, CancellationToken ct) =>
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["credentials"] = ["Username and password are required."] });
        var result = await signIn.PasswordSignInAsync(request.Username, request.Password, request.RememberMe, lockoutOnFailure: true);
        return result.Succeeded ? Results.NoContent() : result.IsLockedOut
            ? Results.Problem(statusCode: 423, title: "Account locked", extensions: new Dictionary<string, object?> { ["code"] = "identity.locked" })
            : Results.Problem(statusCode: 401, title: "Authentication failed", extensions: new Dictionary<string, object?> { ["code"] = "identity.invalid_credentials" });
    }).DisableAntiforgery();
    app.MapPost("/api/auth/logout", async (SignInManager<ApplicationUser> signIn) => { await signIn.SignOutAsync(); return Results.NoContent(); })
        .RequireAuthorization().WithMetadata(new Microsoft.AspNetCore.Antiforgery.RequireAntiforgeryTokenAttribute(true));
    app.MapGet("/api/auth/me", async (ClaimsPrincipal principal, UserManager<ApplicationUser> users) =>
    {
        var user = await users.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();
        return Results.Ok(new { id = user.Id, username = user.UserName, displayName = user.DisplayName, roles = await users.GetRolesAsync(user) });
    }).RequireAuthorization();
}

var api = app.MapGroup("/api");
if (durableMode) api.RequireAuthorization();
api.MapPost("/exams", async (CreateExamRequest request, MastemisService service, CancellationToken ct) =>
{
    var exam = await service.CreateExamAsync(new(request.Title, request.IdempotencyKey), ct);
    return Results.Created($"/api/exams/{exam.Id.Value}", ExamResponse.From(exam));
});
api.MapPost("/exams/{examId:guid}/open", async (Guid examId, MastemisService service, CancellationToken ct) =>
{
    await service.OpenExamAsync(new ExamId(examId), ct);
    return Results.NoContent();
});
api.MapPost("/exams/{examId:guid}/close", async (Guid examId, MastemisService service, CancellationToken ct) =>
{ await service.CloseExamAsync(new ExamId(examId), ct); return Results.NoContent(); });
api.MapPost("/exams/{examId:guid}/cancel", async (Guid examId, MastemisService service, CancellationToken ct) =>
{ await service.CancelExamAsync(new ExamId(examId), ct); return Results.NoContent(); });
api.MapPost("/exams/{examId:guid}/schedule", async (Guid examId, ScheduleExamRequest request, MastemisService service, CancellationToken ct) =>
{ await service.ScheduleExamAsync(new(new ExamId(examId), request.StartsAtUtc, request.EndsAtUtc, request.IdempotencyKey), ct); return Results.NoContent(); });
api.MapPost("/exams/{examId:guid}/rooms", async (Guid examId, CreateRoomRequest request, MastemisService service, CancellationToken ct) =>
{
    var room = await service.CreateRoomAsync(new(new ExamId(examId), request.Name, request.IdempotencyKey), ct);
    return Results.Created($"/api/rooms/{room.Id.Value}", new { id = room.Id.Value, examId = room.ExamId.Value, room.Name });
});
api.MapPost("/exams/{examId:guid}/candidates", async (Guid examId, RegisterCandidateRequest request, MastemisService service, CancellationToken ct) =>
{
    var candidate = await service.RegisterCandidateAsync(new(new ExamId(examId), new UserId(request.UserId), request.RegistrationCode, request.IdempotencyKey), ct);
    return Results.Created($"/api/candidates/{candidate.Id.Value}", new { id = candidate.Id.Value, userId = candidate.UserId.Value, candidate.RegistrationCode });
});
api.MapPost("/sessions", async (StartSessionRequest request, MastemisService service, CancellationToken ct) =>
{
    var session = await service.StartExamSessionAsync(new(new ExamId(request.ExamId), new RoomId(request.RoomId),
        new CandidateId(request.CandidateId), request.IdempotencyKey), ct);
    return Results.Created($"/api/sessions/{session.Id.Value}", SessionResponse.From(session));
});
api.MapGet("/sessions/{sessionId:guid}", async (Guid sessionId, MastemisService service, CancellationToken ct) =>
    SessionResponse.From(await service.GetCandidateSessionAsync(new SessionId(sessionId), ct)));
api.MapPost("/sessions/{sessionId:guid}/drafts", async (Guid sessionId, SaveDraftRequest request, MastemisService service, CancellationToken ct) =>
{
    byte[] content;
    try { content = Convert.FromBase64String(request.ContentBase64); }
    catch (FormatException) { throw new ApplicationFailure(ErrorCodes.InvalidInput, "Source content must be base64 encoded."); }
    var revision = await service.SaveDraftRevisionAsync(new(new SessionId(sessionId), content, request.IdempotencyKey), ct);
    return Results.Created($"/api/sessions/{sessionId}/drafts/{revision.Id.Value}", new { id = revision.Id.Value, revision.Sha256, revision.CreatedAtUtc });
}).DisableAntiforgery();
api.MapPost("/sessions/{sessionId:guid}/submissions", async (Guid sessionId, CreateSubmissionRequest request, MastemisService service, CancellationToken ct) =>
{
    var submission = await service.CreateSubmissionAsync(new(new SessionId(sessionId), new ProblemId(request.ProblemId),
        new SourceRevisionId(request.RevisionId), request.Language, request.IdempotencyKey), ct);
    return Results.Created($"/api/submissions/{submission.Id.Value}", SubmissionResponse.From(submission));
});
api.MapGet("/submissions/{submissionId:guid}", async (Guid submissionId, MastemisService service, CancellationToken ct) =>
    SubmissionResponse.From(await service.GetSubmissionAsync(new SubmissionId(submissionId), ct)));
api.MapGet("/sessions/{sessionId:guid}/submissions", async (Guid sessionId, MastemisService service, CancellationToken ct) =>
    (await service.GetSubmissionHistoryAsync(new SessionId(sessionId), ct)).Select(SubmissionResponse.From));
api.MapPost("/sessions/{sessionId:guid}/sfe-events", async (Guid sessionId, SfeEventRequest request, MastemisService service, CancellationToken ct) =>
{
    var activityEvent = await service.RecordRawSfeEventAsync(new(new SessionId(sessionId), request.ClientSequence,
        request.ClientTimestamp, request.EventType, request.Metadata ?? new Dictionary<string, string>(), request.IdempotencyKey), ct);
    return Results.Accepted($"/api/sfe-events/{activityEvent.Id.Value}", new { id = activityEvent.Id.Value, activityEvent.ServerReceivedAtUtc });
});
api.MapPost("/sfe-events/{eventId:guid}/evaluate", async (Guid eventId, EvaluateSfeRequest request, MastemisService service, CancellationToken ct) =>
    await service.EvaluateSfeEventAsync(new(new ViolationEventId(eventId), request.DurationMilliseconds is { } duration ? TimeSpan.FromMilliseconds(duration) : null, request.ConcurrentSession), ct));
api.MapPost("/sessions/{sessionId:guid}/warnings", async (Guid sessionId, IssueWarningRequest request, MastemisService service, CancellationToken ct) =>
    await service.IssueStoredWarningAsync(new(new SessionId(sessionId), new ViolationEvaluationId(request.EvaluationId),
        new ProblemId(request.ProblemId), request.Language, request.IdempotencyKey), ct));
api.MapGet("/exams/{examId:guid}/summary", async (Guid examId, MastemisService service, CancellationToken ct) =>
    await service.GetExamSummaryAsync(new ExamId(examId), ct));

if (durableMode)
{
    var administration = app.MapGroup("/api/admin").RequireAuthorization("Administrator");
    administration.MapPost("/workers", async (RegisterWorkerRequest request, IWorkerCredentialService credentials, CancellationToken ct) =>
        Results.Created("/api/admin/workers", await credentials.RegisterAsync(request.Name, request.Capacity, request.ExpiresAtUtc, ct)));
    administration.MapPost("/workers/{workerId:guid}/rotate", async (Guid workerId, RotateWorkerRequest request, IWorkerCredentialService credentials, CancellationToken ct) =>
        Results.Ok(await credentials.RotateAsync(new JudgeWorkerId(workerId), request.ExpiresAtUtc, ct)));
    administration.MapDelete("/workers/{workerId:guid}/credential", async (Guid workerId, IWorkerCredentialService credentials, CancellationToken ct) =>
    { await credentials.RevokeAsync(new JudgeWorkerId(workerId), ct); return Results.NoContent(); });

    var workerApi = app.MapGroup("/api/worker").RequireAuthorization("WorkerOnly");
    workerApi.MapPost("/heartbeat", async (ClaimsPrincipal principal, WorkerHeartbeatRequest request, IWorkerCredentialService credentials, CancellationToken ct) =>
    { await credentials.HeartbeatAsync(new JudgeWorkerId(Guid.Parse(principal.FindFirst("worker_id")!.Value)), request.Capacity, ct); return Results.NoContent(); });
    workerApi.MapPost("/jobs/claim", async (ClaimsPrincipal principal, ClaimJobRequest request, IWorkerJudgeQueue queue, CancellationToken ct) =>
        await queue.ClaimAsync(new JudgeWorkerId(Guid.Parse(principal.FindFirst("worker_id")!.Value)), TimeSpan.FromSeconds(request.LeaseSeconds), ct));
    workerApi.MapPost("/jobs/{jobId:guid}/renew", async (Guid jobId, ClaimsPrincipal principal, LeaseRequest request, IWorkerJudgeQueue queue, CancellationToken ct) =>
    { await queue.RenewAsync(new JudgeWorkerId(Guid.Parse(principal.FindFirst("worker_id")!.Value)), new JudgeJobId(jobId), request.LeaseId, TimeSpan.FromSeconds(request.LeaseSeconds), ct); return Results.NoContent(); });
    workerApi.MapPost("/jobs/{jobId:guid}/start", async (Guid jobId, ClaimsPrincipal principal, LeaseRequest request, IWorkerJudgeQueue queue, CancellationToken ct) =>
    { await queue.StartAsync(new JudgeWorkerId(Guid.Parse(principal.FindFirst("worker_id")!.Value)), new JudgeJobId(jobId), request.LeaseId, ct); return Results.NoContent(); });
    workerApi.MapPost("/jobs/{jobId:guid}/complete", async (Guid jobId, ClaimsPrincipal principal, CompleteJobRequest request, IWorkerJudgeQueue queue, IClock clock, CancellationToken ct) =>
    {
        await queue.CompleteAsync(new JudgeWorkerId(Guid.Parse(principal.FindFirst("worker_id")!.Value)), new JudgeJobId(jobId), request.LeaseId,
        new Judgement(new SubmissionId(request.SubmissionId), request.Verdict, request.Score, clock.UtcNow), ct); return Results.NoContent();
    });
    workerApi.MapPost("/jobs/{jobId:guid}/fail", async (Guid jobId, ClaimsPrincipal principal, FailJobRequest request, IWorkerJudgeQueue queue, CancellationToken ct) =>
    { await queue.FailAsync(new JudgeWorkerId(Guid.Parse(principal.FindFirst("worker_id")!.Value)), new JudgeJobId(jobId), request.LeaseId, request.FailureCode, ct); return Results.NoContent(); });
}
app.MapHub<ExamHub>("/hubs/exam");
app.MapOpenApi();
app.Run();

public sealed record CreateExamRequest(string Title, string IdempotencyKey);
public sealed record LoginRequest(string Username, string Password, bool RememberMe);
public sealed record ScheduleExamRequest(DateTimeOffset StartsAtUtc, DateTimeOffset EndsAtUtc, string IdempotencyKey);
public sealed record CreateRoomRequest(string Name, string IdempotencyKey);
public sealed record RegisterCandidateRequest(Guid UserId, string RegistrationCode, string IdempotencyKey);
public sealed record StartSessionRequest(Guid ExamId, Guid RoomId, Guid CandidateId, string IdempotencyKey);
public sealed record SaveDraftRequest(string ContentBase64, string IdempotencyKey);
public sealed record CreateSubmissionRequest(Guid ProblemId, Guid RevisionId, string Language, string IdempotencyKey);
public sealed record SfeEventRequest(long ClientSequence, DateTimeOffset ClientTimestamp, string EventType,
    Dictionary<string, string>? Metadata, string IdempotencyKey);
public sealed record EvaluateSfeRequest(double? DurationMilliseconds, bool ConcurrentSession);
public sealed record IssueWarningRequest(Guid EvaluationId, Guid ProblemId, string Language, string IdempotencyKey);
public sealed record RegisterWorkerRequest(string Name, int Capacity, DateTimeOffset? ExpiresAtUtc);
public sealed record RotateWorkerRequest(DateTimeOffset? ExpiresAtUtc);
public sealed record WorkerHeartbeatRequest(int Capacity);
public sealed record ClaimJobRequest(int LeaseSeconds);
public sealed record LeaseRequest(Guid LeaseId, int LeaseSeconds);
public sealed record CompleteJobRequest(Guid LeaseId, Guid SubmissionId, SubmissionState Verdict, int Score);
public sealed record FailJobRequest(Guid LeaseId, string FailureCode);
public sealed record ExamResponse(Guid Id, string Title, string State)
{
    public static ExamResponse From(Exam exam) => new(exam.Id.Value, exam.Title, exam.State.ToString());
}
public sealed record SessionResponse(Guid Id, Guid ExamId, Guid RoomId, Guid CandidateId, string State, int WarningCount, Guid? FrozenRevisionId)
{
    public static SessionResponse From(ExamSession session) => new(session.Id.Value, session.ExamId.Value, session.RoomId.Value,
        session.CandidateId.Value, session.State.ToString(), session.Warnings.Count, session.FrozenRevisionId?.Value);
}
public sealed record SubmissionResponse(Guid Id, Guid SessionId, Guid ProblemId, Guid RevisionId, string Language, string State, bool IsFinal)
{
    public static SubmissionResponse From(Submission submission) => new(submission.Id.Value, submission.SessionId.Value,
        submission.ProblemId.Value, submission.RevisionId.Value, submission.Language, submission.State.ToString(), submission.IsFinal);
}
public partial class Program;
