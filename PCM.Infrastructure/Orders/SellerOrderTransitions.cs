using PCM.DomainEntities.Enums;
using PCM.Infrastructure.Exceptions;

namespace PCM.Infrastructure.Orders;

/// <summary>Who is changing a sub-order's status. Decides which moves are allowed.</summary>
public enum OrderActor
{
    Customer,
    Seller,
    Admin
}

/// <summary>
/// The sub-order (SellerOrder) status state machine agreed for V1 (business rule 4):
/// <code>
/// Pending   --> Confirmed --> Shipped --> Delivered
/// Pending   --> Cancelled
/// Confirmed --> Cancelled
/// </code>
/// Delivered and Cancelled are final. Sellers and admins may make every move; customers may only
/// cancel. Any other combination is a 409 Conflict.
/// </summary>
public static class SellerOrderTransitions
{
    private static readonly Dictionary<SellerOrderStatus, SellerOrderStatus[]> Next = new()
    {
        [SellerOrderStatus.Pending] = [SellerOrderStatus.Confirmed, SellerOrderStatus.Cancelled],
        [SellerOrderStatus.Confirmed] = [SellerOrderStatus.Shipped, SellerOrderStatus.Cancelled],
        [SellerOrderStatus.Shipped] = [SellerOrderStatus.Delivered],
        [SellerOrderStatus.Delivered] = [],
        [SellerOrderStatus.Cancelled] = []
    };

    /// <summary>True when <paramref name="actor"/> may move a sub-order from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public static bool IsAllowed(SellerOrderStatus from, SellerOrderStatus to, OrderActor actor) =>
        Next[from].Contains(to) && (actor != OrderActor.Customer || to == SellerOrderStatus.Cancelled);

    /// <summary>The statuses the actor can move to from <paramref name="from"/> (drives the buttons in the UI).</summary>
    public static IReadOnlyList<SellerOrderStatus> AllowedNext(SellerOrderStatus from, OrderActor actor) =>
        Next[from].Where(to => IsAllowed(from, to, actor)).ToList();

    /// <summary>Cancelling is possible only before shipping (Pending or Confirmed).</summary>
    public static bool CanCancel(SellerOrderStatus from) =>
        Next[from].Contains(SellerOrderStatus.Cancelled);

    /// <exception cref="ConflictException">The move is not allowed.</exception>
    public static void EnsureAllowed(SellerOrderStatus from, SellerOrderStatus to, OrderActor actor)
    {
        if (IsAllowed(from, to, actor))
        {
            return;
        }

        if (to == SellerOrderStatus.Cancelled)
        {
            throw new ConflictException($"This part of the order is {from} and can no longer be cancelled.");
        }

        if (actor == OrderActor.Customer)
        {
            throw new ConflictException("Customers can only cancel an order.");
        }

        var allowed = Next[from];
        throw new ConflictException(allowed.Length == 0
            ? $"This part of the order is {from}; its status cannot change any more."
            : $"Cannot move from {from} to {to}. Next allowed: {string.Join(" or ", allowed)}.");
    }
}
