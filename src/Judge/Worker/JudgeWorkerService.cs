using Mastemis.Contracts.Judge;
using Mastemis.Domain;
using Mastemis.Judge.Configuration;
using Mastemis.Judge.Execution;
using Mastemis.Sandbox.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Mastemis.Judge.Worker;

public sealed class JudgeWorkerService(IJudgeServerClient server, IJudgementOrchestrator orchestrator,
    ISandboxCapabilityProbe sandboxProbe, JudgeWorkerOptions options, JudgeWorkerHealthState health,
    ILogger<JudgeWorkerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        options.Validate(); var capabilities = await sandboxProbe.ProbeAsync(stoppingToken);
        var workspaceReady = ProbeWorkspace();
        health.Update(state => state with
        {
            Sandbox = capabilities,
            CppAvailable = File.Exists("/usr/bin/g++"),
            DotnetAvailable = File.Exists("/usr/bin/dotnet"),
            WorkspaceWritable = workspaceReady,
            Capacity = options.Capacity
        });
        if (!capabilities.MeetsMandatoryRequirements || !workspaceReady || !health.Snapshot.CppAvailable || !health.Snapshot.DotnetAvailable)
        {
            logger.LogError("Judge worker cannot start job processing because a mandatory local capability is unavailable: {ReasonCode}",
                "worker.capability_unavailable");
            return;
        }
        var active = new HashSet<Task>(); var nextHeartbeat = DateTimeOffset.MinValue;
        while (!stoppingToken.IsCancellationRequested)
        {
            active.RemoveWhere(task => task.IsCompleted);
            if (DateTimeOffset.UtcNow >= nextHeartbeat)
            {
                await HeartbeatAsync(active.Count, stoppingToken); nextHeartbeat = DateTimeOffset.UtcNow + options.HeartbeatInterval;
            }
            while (active.Count < options.Capacity)
            {
                var lease = await server.ClaimAsync((int)options.LeaseDuration.TotalSeconds, stoppingToken);
                if (lease is null) break;
                var task = ProcessLeaseAsync(lease, stoppingToken); active.Add(task);
            }
            health.Update(state => state with { ActiveJobs = active.Count });
            await Task.Delay(options.ClaimInterval, stoppingToken);
        }
        await DrainAsync(active);
    }

    private async Task ProcessLeaseAsync(WorkerLeaseContract lease, CancellationToken stoppingToken)
    {
        using var execution = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var renewal = RenewLeaseAsync(lease, execution.Token);
        try
        {
            var contract = await server.GetContractAsync(lease.JobId, lease.LeaseId, execution.Token);
            if (contract.JobId != lease.JobId || contract.SubmissionId != lease.SubmissionId) throw new JudgeContractException(JudgeFailureCode.InvalidContract, "Server job identity mismatch.");
            var source = await server.GetSourceAsync(lease.JobId, lease.LeaseId, 1_048_576, execution.Token);
            var tests = new List<TestCaseDescriptor>(contract.Tests.Count);
            foreach (var test in contract.Tests.OrderBy(x => x.Index))
            {
                var input = await server.GetTestInputAsync(lease.JobId, lease.LeaseId, test.Index, test.InputBytes, execution.Token);
                var expected = await server.GetExpectedOutputAsync(lease.JobId, lease.LeaseId, test.Index, test.ExpectedOutputBytes, execution.Token);
                tests.Add(new(test.Index, input, expected, test.CheckerId));
            }
            await server.StartAsync(lease.JobId, lease.LeaseId, execution.Token);
            var fileName = contract.LanguageId.Equals("cpp", StringComparison.OrdinalIgnoreCase) ? "main.cpp" : "Program.cs";
            var result = await orchestrator.ExecuteAsync(new(lease.JobId, lease.SubmissionId, options.WorkerId, contract.LanguageId,
                [new(fileName, source)], tests, contract.Limits, new(System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
                    new Dictionary<string, string> { ["LANG"] = "C.UTF-8" })), execution.Token);
            await server.CompleteAsync(lease.JobId, new(lease.LeaseId, lease.SubmissionId, result), execution.Token);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception exception) when (exception is JudgeContractException or JudgeServerException or HttpRequestException)
        {
            var code = exception is JudgeContractException contract ? contract.Code.ToString() : "worker.infrastructure";
            health.Update(state => state with { LastFailureCode = code });
            try { await server.FailAsync(lease.JobId, new(lease.LeaseId, code), CancellationToken.None); }
            catch (Exception reportException) when (reportException is HttpRequestException or JudgeServerException)
            { logger.LogWarning("Unable to report judge job failure; lease recovery will handle the abandoned job."); }
        }
        finally
        {
            execution.Cancel();
            try { await renewal; }
            catch (OperationCanceledException) { }
            catch (Exception exception) when (exception is JudgeServerException or HttpRequestException)
            { logger.LogWarning("Lease renewal stopped after a server communication failure."); }
        }
    }

    private async Task RenewLeaseAsync(WorkerLeaseContract lease, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(options.LeaseRenewalInterval);
        while (await timer.WaitForNextTickAsync(cancellationToken))
            await server.RenewAsync(lease.JobId, new(lease.LeaseId, (int)options.LeaseDuration.TotalSeconds), cancellationToken);
    }
    private async Task HeartbeatAsync(int active, CancellationToken cancellationToken)
    {
        try
        {
            await server.HeartbeatAsync(new(options.Capacity, ["cpp", "csharp"], "podman"), cancellationToken);
            health.Update(state => state with
            {
                ServerConnected = true,
                Authenticated = true,
                ActiveJobs = active,
                LastHeartbeatUtc = DateTimeOffset.UtcNow,
                LastFailureCode = null
            });
        }
        catch (JudgeServerException exception) { health.Update(state => state with { ServerConnected = true, Authenticated = false, LastFailureCode = exception.Code }); }
        catch (HttpRequestException) { health.Update(state => state with { ServerConnected = false, Authenticated = false, LastFailureCode = "worker.server_unavailable" }); }
    }
    private bool ProbeWorkspace()
    {
        try { Directory.CreateDirectory(options.WorkspaceRoot); var path = Path.Combine(options.WorkspaceRoot, $".probe-{Guid.NewGuid():N}"); File.WriteAllBytes(path, []); File.Delete(path); return true; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return false; }
    }
    private async Task DrainAsync(IEnumerable<Task> active)
    {
        using var timeout = new CancellationTokenSource(options.ShutdownTimeout);
        try { await Task.WhenAll(active).WaitAsync(timeout.Token); } catch (OperationCanceledException) { }
    }
}
