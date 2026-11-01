using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;
using PCM.API.Caching;
using PCM.Infrastructure.Orders;

namespace PCM.API.BackgroundJobs;

/// <summary>"Orders:AutoCancel" configuration section. Validated at startup.</summary>
public sealed class PendingOrderExpiryOptions
{
    public const string SectionName = "Orders:AutoCancel";

    /// <summary>Off in the integration tests, which call <see cref="PendingOrderExpiry"/> directly.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>A Pending sub-order older than this is cancelled (agreed: 3 days).</summary>
    [Range(1, 60)]
    public int PendingDays { get; set; } = 3;

    /// <summary>How often the job looks for expired sub-orders.</summary>
    [Range(1, 1440)]
    public int IntervalMinutes { get; set; } = 60;

    /// <summary>Most sub-orders handled per run; the rest wait for the next run.</summary>
    [Range(1, 1000)]
    public int BatchSize { get; set; } = 100;
}

/// <summary>
/// Hosted service: every <see cref="PendingOrderExpiryOptions.IntervalMinutes"/> it cancels sub-orders
/// still Pending after <see cref="PendingOrderExpiryOptions.PendingDays"/> days, releasing their stock.
/// The work itself is in <see cref="PendingOrderExpiry"/> (Infrastructure); this class only schedules it.
/// </summary>
public sealed class PendingOrderExpiryJob(
    IServiceScopeFactory scopeFactory,
    IOptions<PendingOrderExpiryOptions> options,
    CatalogCacheInvalidator catalogCache,
    ILogger<PendingOrderExpiryJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            logger.LogInformation("Auto-cancel of unconfirmed orders is disabled");
            return;
        }

        // First run right away, then on a fixed interval. PeriodicTimer never overlaps runs.
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(settings.IntervalMinutes));
        do
        {
            await RunOnceAsync(settings, stoppingToken);
        }
        while (await WaitAsync(timer, stoppingToken));
    }

    private async Task RunOnceAsync(PendingOrderExpiryOptions settings, CancellationToken stoppingToken)
    {
        try
        {
            // DbContext and the unit of work are scoped: one fresh scope per run.
            await using var scope = scopeFactory.CreateAsyncScope();
            var expiry = scope.ServiceProvider.GetRequiredService<PendingOrderExpiry>();
            var cancelled = await expiry.CancelExpiredAsync(settings.PendingDays, settings.BatchSize, stoppingToken);

            // Cancelling released reserved stock: product pages and "in stock" filters must show it.
            if (cancelled > 0)
            {
                await catalogCache.EvictAsync(CatalogCacheScope.Products, CancellationToken.None);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
        catch (Exception ex)
        {
            // A failed run (e.g. database down) must not stop the job; the next tick tries again.
            logger.LogError(ex, "Auto-cancel of unconfirmed orders failed");
        }
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
