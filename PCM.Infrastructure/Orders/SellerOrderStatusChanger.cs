using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PCM.DomainEntities.DBEntities;
using PCM.DomainEntities.Enums;
using PCM.Repositories;

namespace PCM.Infrastructure.Orders;

/// <summary>
/// Runs one status move (customer cancel, seller or admin move) on one or more sub-orders and saves
/// once, so status, stock and stock movements commit together (SaveChanges is one transaction).
/// </summary>
/// <remarks>
/// SellerOrder and Offer both carry a RowVersion. If another request changed one of the rows in
/// between (the seller ships while the customer cancels, or a checkout reserves the same offer),
/// the save fails with a concurrency error: the tracked state is discarded and the whole step runs
/// again on fresh rows, so the move is re-checked against the new status. A second conflict is
/// left to the API, which answers 409.
/// </remarks>
public sealed class SellerOrderStatusChanger(
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<SellerOrderStatusChanger> logger)
{
    /// <summary>First try plus one retry after a concurrency conflict.</summary>
    public const int MaxSaveAttempts = 2;

    /// <param name="load">Loads the tracked sub-orders to change (with Order and OrderItems); throws 404 / 409 itself. Runs again on a retry.</param>
    /// <param name="to">Target status.</param>
    /// <param name="actor">Who makes the move.</param>
    /// <param name="reason">Cancel reason, if any.</param>
    /// <param name="userId">Current user, recorded on the stock movements.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    public async Task ChangeAsync(
        Func<CancellationToken, Task<IReadOnlyList<SellerOrder>>> load,
        SellerOrderStatus to,
        OrderActor actor,
        string? reason,
        int userId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(load);

        for (var attempt = 1; ; attempt++)
        {
            var subOrders = await load(cancellationToken);
            var now = timeProvider.GetUtcNow().UtcDateTime;

            IReadOnlyDictionary<int, Offer> offers = new Dictionary<int, Offer>();
            if (SellerOrderWorkflow.ChangesStock(to))
            {
                var offerIds = subOrders.SelectMany(s => s.OrderItems).Select(i => i.OfferId).Distinct().ToList();
                offers = (await unitOfWork.Offers.GetForUpdateAsync(offerIds, cancellationToken)).ToDictionary(o => o.Id);
            }

            foreach (var subOrder in subOrders)
            {
                SellerOrderWorkflow.Apply(subOrder, offers, to, actor, reason, userId, now);

                // Marks the order as changed; the audit interceptor stamps UpdatedAt on save.
                subOrder.Order.UpdatedAt = now;
            }

            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
                logger.LogInformation("{Actor} {UserId} moved sub-order(s) {SellerOrderIds} to {Status}",
                    actor, userId, string.Join(",", subOrders.Select(s => s.Id)), to);
                return;
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxSaveAttempts)
            {
                logger.LogWarning("Concurrency conflict moving sub-order(s) {SellerOrderIds} to {Status} (attempt {Attempt}); retrying with fresh data",
                    string.Join(",", subOrders.Select(s => s.Id)), to, attempt);
                unitOfWork.ClearChanges();
            }
        }
    }
}
