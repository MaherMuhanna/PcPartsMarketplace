using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PCM.DomainEntities.DBEntities;
using PCM.DomainEntities.Enums;
using PCM.Infrastructure.Exceptions;
using PCM.Repositories;

namespace PCM.Infrastructure.Orders;

/// <summary>
/// Cancels sub-orders that a seller never confirmed, so their reserved stock goes back on sale.
/// With cash on delivery nothing is paid up front, so an unconfirmed order only blocks stock;
/// real marketplaces drop it after a few days. Run by the PendingOrderExpiryJob in PCM.API.
/// </summary>
/// <remarks>
/// Each sub-order is cancelled on its own through <see cref="SellerOrderStatusChanger"/> (status,
/// Release movements and stock in one save), so one failure never blocks the rest. If the seller
/// confirms (or the customer cancels) at the same moment, the RowVersion conflict or the status
/// check makes the job skip that sub-order: the person always wins over the job. Safe to run on
/// several API instances at once for the same reason.
/// </remarks>
public sealed class PendingOrderExpiry(
    IUnitOfWork unitOfWork,
    SellerOrderStatusChanger statusChanger,
    TimeProvider timeProvider,
    ILogger<PendingOrderExpiry> logger)
{
    /// <summary>The cancel reason customers and sellers see ("System: Not confirmed within 3 days").</summary>
    public static string ReasonFor(int pendingDays) =>
        $"Not confirmed within {pendingDays} day{(pendingDays == 1 ? string.Empty : "s")}";

    /// <summary>
    /// Cancels up to <paramref name="batchSize"/> Pending sub-orders older than <paramref name="pendingDays"/> days.
    /// </summary>
    /// <returns>How many sub-orders were cancelled.</returns>
    public async Task<int> CancelExpiredAsync(int pendingDays, int batchSize, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pendingDays, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);

        var cutoff = timeProvider.GetUtcNow().UtcDateTime.AddDays(-pendingDays);
        var ids = await unitOfWork.SellerOrders.GetPendingIdsCreatedBeforeAsync(cutoff, batchSize, cancellationToken);
        var reason = ReasonFor(pendingDays);
        var cancelled = 0;

        foreach (var id in ids)
        {
            try
            {
                await statusChanger.ChangeAsync(
                    ct => LoadAsync(id, ct),
                    SellerOrderStatus.Cancelled,
                    OrderActor.System,
                    reason,
                    userId: null,
                    cancellationToken);
                cancelled++;
            }
            catch (Exception ex) when (ex is ConflictException or DbUpdateConcurrencyException)
            {
                // Confirmed or cancelled by someone else in the meantime: nothing to do.
                logger.LogInformation("Auto-cancel skipped sub-order {SellerOrderId}: {Reason}", id, ex.Message);
            }
            finally
            {
                // Start every sub-order from a clean change tracker (also after a failed save).
                unitOfWork.ClearChanges();
            }
        }

        if (cancelled > 0)
        {
            logger.LogInformation("Auto-cancelled {Count} sub-order(s) not confirmed within {Days} day(s)", cancelled, pendingDays);
        }

        return cancelled;
    }

    /// <summary>The sub-order to cancel (tracked, with order and lines); none if it no longer exists.</summary>
    private async Task<IReadOnlyList<SellerOrder>> LoadAsync(int id, CancellationToken cancellationToken)
    {
        var subOrder = await unitOfWork.SellerOrders.GetForUpdateAsync(id, cancellationToken);
        return subOrder is null ? [] : [subOrder];
    }
}
