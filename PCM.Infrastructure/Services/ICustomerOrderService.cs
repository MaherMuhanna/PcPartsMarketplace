using PCM.Infrastructure.DTO.Common;
using PCM.Infrastructure.DTO.Orders;

namespace PCM.Infrastructure.Services;

/// <summary>The signed-in customer's orders. Another customer's order id is "not found" (404).</summary>
public interface ICustomerOrderService
{
    Task<PagedResponse<OrderListItemDto>> GetOrdersAsync(int userId, OrderListQuery query, CancellationToken cancellationToken = default);

    Task<OrderDto> GetOrderAsync(int userId, int id, CancellationToken cancellationToken = default);

    /// <summary>Cancels every part of the order that is still Pending or Confirmed; 409 when none is.</summary>
    Task<OrderDto> CancelOrderAsync(int userId, int id, CancelOrderRequest request, CancellationToken cancellationToken = default);

    /// <summary>Cancels one seller's part of the order (Pending or Confirmed only, else 409).</summary>
    Task<OrderDto> CancelSellerOrderAsync(int userId, int orderId, int sellerOrderId, CancelOrderRequest request, CancellationToken cancellationToken = default);
}
