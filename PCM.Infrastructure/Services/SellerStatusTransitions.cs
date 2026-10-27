using PCM.DomainEntities.Enums;
using PCM.Infrastructure.Exceptions;

namespace PCM.Infrastructure.Services;

/// <summary>Admin actions on a seller.</summary>
public enum SellerAction
{
    Approve,
    Reject,
    Suspend,
    Reinstate
}

/// <summary>
/// The seller status state machine agreed for V1:
/// <code>
/// Pending   --Approve-->   Approved
/// Pending   --Reject--->   Rejected     (final in V1: no re-application)
/// Approved  --Suspend-->   Suspended
/// Suspended --Reinstate--> Approved
/// </code>
/// Any other combination is a 409 Conflict.
/// </summary>
public static class SellerStatusTransitions
{
    private static readonly Dictionary<SellerAction, (SellerStatus From, SellerStatus To)> Rules = new()
    {
        [SellerAction.Approve] = (SellerStatus.Pending, SellerStatus.Approved),
        [SellerAction.Reject] = (SellerStatus.Pending, SellerStatus.Rejected),
        [SellerAction.Suspend] = (SellerStatus.Approved, SellerStatus.Suspended),
        [SellerAction.Reinstate] = (SellerStatus.Suspended, SellerStatus.Approved)
    };

    /// <summary>Returns the new status, or throws <see cref="ConflictException"/> if the action is not allowed.</summary>
    public static SellerStatus Apply(SellerStatus current, SellerAction action)
    {
        var (from, to) = Rules[action];

        if (current != from)
        {
            throw new ConflictException(
                $"Cannot {action.ToString().ToLowerInvariant()} a seller whose status is {current}; " +
                $"this action requires status {from}.");
        }

        return to;
    }

    /// <summary>True when the action is allowed from the current status (used to show only valid buttons).</summary>
    public static bool IsAllowed(SellerStatus current, SellerAction action) => Rules[action].From == current;
}
