using PCM.Infrastructure.DTO.Common;
using PCM.Infrastructure.DTO.Orders;

namespace PCM.Infrastructure.Services;

/// <summary>The signed-in (approved) seller's sub-orders. Another seller's sub-order id is "not found" (404).</summary>
public interface ISellerOrderService
{
    Task<PagedResponse<SellerOrderListItemDto>> GetOrdersAsync(int userId, SellerOrderListQuery query, CancellationToken cancellationToken = default);

    Task<SellerOrderDetailsDto> GetOrderAsync(int userId, int id, CancellationToken cancellationToken = default);

    /// <summary>Next step of the flow (Confirmed, Shipped, Delivered) or Cancelled with a reason; invalid moves are 409.</summary>
    Task<SellerOrderDetailsDto> ChangeStatusAsync(int userId, int id, ChangeSellerOrderStatusRequest request, CancellationToken cancellationToken = default);
}
