using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Mastemis.Infrastructure.Storage.Reconciliation;

public sealed class SourceObjectReconciliationService(IServiceScopeFactory scopeFactory, SourceReconciliationOptions options,
    SourceReconciliationStatus status, ILogger<SourceObjectReconciliationService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.ScanInterval);
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<SourceObjectReconciler>().ReconcileAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                status.MarkFailure();
                logger.LogError(exception, "Source object reconciliation failed; no unverified objects will be removed.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
