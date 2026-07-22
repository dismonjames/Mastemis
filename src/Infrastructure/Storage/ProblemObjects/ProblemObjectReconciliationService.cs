using Mastemis.Application;
using Mastemis.Infrastructure.Storage.ProblemObjects.Exports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Mastemis.Infrastructure.Storage.ProblemObjects;

public sealed class ProblemObjectReconciliationService(IServiceScopeFactory scopes, ProblemObjectReconciliationOptions options,
    ProblemObjectReconciliationStatus status, IClock clock, ILogger<ProblemObjectReconciliationService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.ScanInterval);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ProblemObjectReconciler>()
                    .ReconcileAsync(clock.UtcNow - options.OrphanAge, options.BoundedBatchSize, stoppingToken);
                await scope.ServiceProvider.GetRequiredService<ProblemExportCleanupService>()
                    .CleanupAsync(clock.UtcNow, stoppingToken);
                status.MarkSuccess(clock.UtcNow);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                status.MarkFailure();
                logger.LogError(exception, "Problem object reconciliation failed; cleanup is suspended until references can be verified.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
