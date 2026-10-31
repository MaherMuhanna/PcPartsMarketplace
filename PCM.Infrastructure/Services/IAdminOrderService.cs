using PCM.Infrastructure.DTO.Common;
using PCM.Infrastructure.DTO.Orders;

namespace PCM.Infrastructure.Services;

/// <summary>Every order, for admins (e.g. to cancel the open orders of a suspended seller).</summary>
public interface IAdminOrderService
{
    Task<PagedResponse<OrderListItemDto>> GetOrdersAsync(AdminOrderListQuery query, CancellationToken cancellationToken = default);

    Task<OrderDto> GetOrderAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Any allowed move on one sub-order (same state machine as sellers; cancelling needs a reason).</summary>
    Task<OrderDto> ChangeStatusAsync(int adminUserId, int orderId, int sellerOrderId, ChangeSellerOrderStatusRequest request, CancellationToken cancellationToken = default);
}
