using PCM.DomainEntities.Enums;
using PCM.Infrastructure.Exceptions;
using PCM.Infrastructure.Services;

namespace PCM.Infrastructure.Tests.Services;

public sealed class SellerStatusTransitionsTests
{
    [Theory]
    [InlineData(SellerStatus.Pending, SellerAction.Approve, SellerStatus.Approved)]
    [InlineData(SellerStatus.Pending, SellerAction.Reject, SellerStatus.Rejected)]
    [InlineData(SellerStatus.Approved, SellerAction.Suspend, SellerStatus.Suspended)]
    [InlineData(SellerStatus.Suspended, SellerAction.Reinstate, SellerStatus.Approved)]
    public void Allowed_transitions_return_the_new_status(SellerStatus current, SellerAction action, SellerStatus expected)
    {
        Assert.True(SellerStatusTransitions.IsAllowed(current, action));
        Assert.Equal(expected, SellerStatusTransitions.Apply(current, action));
    }

    [Theory]
    [InlineData(SellerStatus.Approved, SellerAction.Approve)]
    [InlineData(SellerStatus.Rejected, SellerAction.Approve)]     // rejection is final in V1
    [InlineData(SellerStatus.Suspended, SellerAction.Approve)]    // use Reinstate instead
    [InlineData(SellerStatus.Approved, SellerAction.Reject)]
    [InlineData(SellerStatus.Pending, SellerAction.Suspend)]
    [InlineData(SellerStatus.Rejected, SellerAction.Suspend)]
    [InlineData(SellerStatus.Pending, SellerAction.Reinstate)]
    [InlineData(SellerStatus.Rejected, SellerAction.Reinstate)]
    public void Other_transitions_are_conflicts(SellerStatus current, SellerAction action)
    {
        Assert.False(SellerStatusTransitions.IsAllowed(current, action));
        Assert.Throws<ConflictException>(() => SellerStatusTransitions.Apply(current, action));
    }
}
