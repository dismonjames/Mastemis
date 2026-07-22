using Mastemis.Contracts.Judge;
using Mastemis.Contracts.Problems.ReferenceOutputs;
using Mastemis.Judge.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Mastemis.Judge.Worker.ReferenceOutputs;

public sealed class ReferenceOutputWorkerService(IReferenceOutputServerClient server, ReferenceOutputExecutor executor,
    JudgeWorkerOptions options, ILogger<ReferenceOutputWorkerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var lease = await server.ClaimAsync((int)options.LeaseDuration.TotalSeconds, stoppingToken);
                if (lease is null) { await Task.Delay(options.ClaimInterval, stoppingToken); continue; }
                await ExecuteLeaseAsync(lease, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) when (exception is HttpRequestException or JudgeServerException)
            {
                logger.LogWarning("Reference output worker server operation failed with {FailureCode}.",
                    exception is JudgeServerException serverException ? serverException.Code : "worker.server_unavailable");
                await Task.Delay(options.ClaimInterval, stoppingToken);
            }
        }
    }

    private async Task ExecuteLeaseAsync(ReferenceOutputJobLease lease, CancellationToken stoppingToken)
    {
        using var leaseLost = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var renewal = RenewAsync(lease, leaseLost);
        try
        {
            await executor.ExecuteAsync(lease, leaseLost.Token);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception exception) when (exception is JudgeContractException or IOException or UnauthorizedAccessException)
        {
            var code = exception is JudgeContractException contract ? contract.Code.ToString() : JudgeFailureCode.SandboxFailure.ToString();
            try { await server.FailAsync(lease.JobId, new(lease.JobId, lease.OperationId, lease.WorkerId, lease.LeaseToken, code, "Reference output generation failed."), stoppingToken); }
            catch (Exception reportException) when (reportException is HttpRequestException or JudgeServerException)
            { logger.LogWarning("Reference output failure report could not be delivered for job {JobId}.", lease.JobId); }
        }
        finally
        {
            leaseLost.Cancel();
            try { await renewal; } catch (OperationCanceledException) { }
        }
    }

    private async Task RenewAsync(ReferenceOutputJobLease lease, CancellationTokenSource leaseLost)
    {
        using var timer = new PeriodicTimer(options.LeaseRenewalInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(leaseLost.Token))
                await server.RenewAsync(lease.JobId, lease.LeaseToken, (int)options.LeaseDuration.TotalSeconds, leaseLost.Token);
        }
        catch (Exception exception) when (exception is HttpRequestException or JudgeServerException)
        {
            logger.LogWarning("Reference output lease was lost for job {JobId}.", lease.JobId);
            leaseLost.Cancel();
        }
    }
}
