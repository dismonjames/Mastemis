using System.Diagnostics;
using System.Threading.RateLimiting;
using Mastemis.Application;
using Mastemis.Application.Administration;
using Mastemis.Domain;
using Mastemis.Infrastructure;
using Mastemis.Infrastructure.Persistence;
using Mastemis.Infrastructure.Persistence.Identity;
using Mastemis.Server.Authorization;
using Mastemis.Server.Endpoints.Administration;
using Mastemis.Server.Endpoints.Auth;
using Mastemis.Server.Endpoints.Examinations;
using Mastemis.Server.Endpoints.Workers;
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
    builder.Services.Configure<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, options =>
    {
        options.Cookie.Name = "Mastemis.Session"; options.Cookie.HttpOnly = true; options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict; options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(Math.Clamp(configuredMinutes, 5, 720)); options.LoginPath = "/api/auth/login";
        options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
        options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
    });
    builder.Services.AddScoped<PostgresRuntime>();
    builder.Services.AddScoped<IAggregateStore>(sp => sp.GetRequiredService<PostgresRuntime>());
    builder.Services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<PostgresRuntime>());
    builder.Services.AddScoped<IDurableJudgeQueue, LegacyDurableJudgeQueue>();
    builder.Services.AddScoped<ITransactionalOutbox>(sp => sp.GetRequiredService<PostgresRuntime>());
    builder.Services.AddScoped<IWorkerJudgeQueue, PostgresWorkerJudgeQueue>();
    builder.Services.AddScoped<IWorkerCredentialService, WorkerCredentialService>();
    builder.Services.AddScoped<IHumanIdentityAdministration, HumanIdentityAdministration>();
    builder.Services.AddScoped<IScopeAdministration, ScopeAdministration>();
    builder.Services.AddScoped<IAdministrationActor, HttpAdministrationActor>();
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
    context.Response.Headers.XContentTypeOptions = "nosniff"; context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers.ContentSecurityPolicy = "default-src 'self'; frame-ancestors 'none'";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["X-Correlation-ID"] = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
    await next(context);
});
app.UseRateLimiter(); app.UseAuthentication(); app.UseAuthorization(); app.UseAntiforgery();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = check => check.Tags.Contains("live") });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.MapGet("/api/system/version", () => Results.Ok(new
{
    product = "Mastemis",
    version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0",
    telemetry = "none"
}));
if (durableMode) { app.MapAuthenticationEndpoints(); app.MapAdministrationEndpoints(); }
app.MapExaminationEndpoints(durableMode);
if (durableMode) app.MapWorkerEndpoints();
app.MapHub<ExamHub>("/hubs/exam");
app.MapOpenApi();
app.Run();

public partial class Program;
