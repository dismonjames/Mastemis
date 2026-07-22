using Mastemis.Judge.Checking;
using Mastemis.Judge.Configuration;
using Mastemis.Judge.Execution;
using Mastemis.Judge.Languages;
using Mastemis.Judge.Languages.Cpp;
using Mastemis.Judge.Languages.CSharp;
using Mastemis.Judge.Worker;
using Mastemis.Judge.Workspaces;
using Mastemis.Sandbox.Abstractions;
using Mastemis.Sandbox.Linux;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
var configuration = builder.Configuration;
var workerId = Guid.TryParse(configuration["Worker:Id"], out var configuredWorker) ? configuredWorker : Guid.Empty;
var serverUrl = Uri.TryCreate(configuration["Worker:ServerUrl"], UriKind.Absolute, out var configuredServer) ? configuredServer : new Uri("http://127.0.0.1");
var workspace = Path.GetFullPath(configuration["Worker:WorkspaceRoot"] ?? Path.Combine(AppContext.BaseDirectory, "workspaces"));
var workerOptions = new JudgeWorkerOptions(serverUrl, new(workerId), configuration["Worker:Secret"] ?? string.Empty,
    int.TryParse(configuration["Worker:Capacity"], out var capacity) ? capacity : 1, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10),
    TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(30), workspace);
var sandboxOptions = new OciSandboxOptions(configuration["Sandbox:RuntimePath"] ?? "/usr/bin/podman",
    configuration["Sandbox:Image"] ?? "localhost/mastemis-judge:0.1.0", configuration["Sandbox:ContainerUser"] ?? "1000:1000");
builder.Services.AddSingleton(workerOptions); builder.Services.AddSingleton(sandboxOptions);
builder.Services.AddSingleton<JudgeWorkerHealthState>(); builder.Services.AddSingleton<IJudgeClock, SystemJudgeClock>();
builder.Services.AddSingleton<IJudgeWorkspaceManager>(_ => new JudgeWorkspaceManager(workspace));
builder.Services.AddSingleton<ISandboxCapabilityProbe, OciSandboxCapabilityProbe>(); builder.Services.AddSingleton<ISandboxRunner, OciSandboxRunner>();
builder.Services.AddSingleton<ICompilerProcessRunner, CompilerProcessRunner>();
builder.Services.AddSingleton(new CppLanguageOptions(configuration["Toolchains:Cpp"] ?? "/usr/bin/g++"));
builder.Services.AddSingleton(new CSharpLanguageOptions(configuration["Toolchains:Dotnet"] ?? "/usr/bin/dotnet"));
builder.Services.AddSingleton<ILanguageAdapter, CppLanguageAdapter>(); builder.Services.AddSingleton<ILanguageAdapter, CSharpLanguageAdapter>();
builder.Services.AddSingleton<IOutputChecker, ExactOutputChecker>(); builder.Services.AddSingleton<IOutputChecker, TokenOutputChecker>();
builder.Services.AddSingleton(new JudgeOrchestratorOptions(sandboxOptions.Image, TimeSpan.FromMinutes(30), 64 * 1024 * 1024, "mastemis-judge/0.1"));
builder.Services.AddSingleton<IJudgementOrchestrator, JudgementOrchestrator>(); builder.Services.AddHttpClient<IJudgeServerClient, JudgeServerClient>();
builder.Services.AddHostedService<JudgeWorkerService>();
await builder.Build().RunAsync();
