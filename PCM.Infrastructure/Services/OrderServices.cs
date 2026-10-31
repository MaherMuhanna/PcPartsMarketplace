using PCM.DomainEntities.DBEntities;
using PCM.DomainEntities.Enums;
using PCM.Infrastructure.DTO.Common;
using PCM.Infrastructure.DTO.Orders;
using PCM.Infrastructure.Exceptions;
using PCM.Infrastructure.Mappings;
using PCM.Infrastructure.Orders;
using PCM.Infrastructure.Validators;
using PCM.Repositories;
using PCM.Repositories.Models;

namespace PCM.Infrastructure.Services;

// The three views of the same orders: customer (own orders), seller (own sub-orders) and admin (all).
// Status moves for all three go through SellerOrderStatusChanger, so the rules and side effects are shared.

public sealed class CustomerOrderService(
    IUnitOfWork unitOfWork,
    SellerOrderStatusChanger statusChanger) : ICustomerOrderService
{
    public async Task<PagedResponse<OrderListItemDto>> GetOrdersAsync(int userId, OrderListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var page = await unitOfWork.Orders.GetCustomerPagedAsync(
            userId, query.Page, query.PageSize, OrderRules.IsNewestFirst(query.Sort), cancellationToken);
        return page.ToResponse(row => row.ToDto(customer: null));
    }

    public async Task<OrderDto> GetOrderAsync(int userId, int id, CancellationToken cancellationToken = default)
    {
        var order = await unitOfWork.Orders.GetDetailsAsync(id, userId, cancellationToken)
            ?? throw new NotFoundException($"Order {id} was not found.");
        return order.ToDto(OrderActor.Customer, customer: null);
    }

    public async Task<OrderDto> CancelOrderAsync(int userId, int id, CancelOrderRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await statusChanger.ChangeAsync(async ct =>
        {
            var order = await LoadOwnedAsync(userId, id, ct);
            var open = order.SellerOrders
                .Where(s => SellerOrderTransitions.CanCancel(Enum.Parse<SellerOrderStatus>(s.Status)))
                .ToList();

            return open.Count > 0
                ? open
                : throw new ConflictException("Nothing left to cancel: every part of this order has shipped, been delivered or been cancelled.");
        }, SellerOrderStatus.Cancelled, OrderActor.Customer, request.Reason, userId, cancellationToken);

        return await GetOrderAsync(userId, id, cancellationToken);
    }

    public async Task<OrderDto> CancelSellerOrderAsync(
        int userId, int orderId, int sellerOrderId, CancelOrderRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await statusChanger.ChangeAsync(async ct =>
        {
            var order = await LoadOwnedAsync(userId, orderId, ct);
            var subOrder = order.SellerOrders.FirstOrDefault(s => s.Id == sellerOrderId)
                ?? throw new NotFoundException($"Part {sellerOrderId} of order {orderId} was not found.");
            return [subOrder];
        }, SellerOrderStatus.Cancelled, OrderActor.Customer, request.Reason, userId, cancellationToken);

        return await GetOrderAsync(userId, orderId, cancellationToken);
    }

    private async Task<Order> LoadOwnedAsync(int userId, int id, CancellationToken cancellationToken) =>
        await unitOfWork.Orders.GetForUpdateAsync(id, userId, cancellationToken)
            ?? throw new NotFoundException($"Order {id} was not found.");
}

public sealed class SellerOrderService(
    IUnitOfWork unitOfWork,
    SellerOrderStatusChanger statusChanger,
    CustomerDirectory customers) : ISellerOrderService
{
    public async Task<PagedResponse<SellerOrderListItemDto>> GetOrdersAsync(int userId, SellerOrderListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var sellerId = await GetSellerIdAsync(userId, cancellationToken);

        // The validator already checked the value; this maps "shipped" to the CHECK value "Shipped".
        string? status = OrderRules.TryParseStatus(query.Status, out var parsed) ? parsed.ToString() : null;

        var page = await unitOfWork.SellerOrders.GetSellerPagedAsync(
            sellerId, status, query.Page, query.PageSize, OrderRules.IsNewestFirst(query.Sort), cancellationToken);

        var names = await customers.GetAsync(page.Items.Select(r => r.CustomerId), cancellationToken);
        return page.ToResponse(row => row.ToDto(NameOf(names, row.CustomerId)));
    }

    public async Task<SellerOrderDetailsDto> GetOrderAsync(int userId, int id, CancellationToken cancellationToken = default)
    {
        var sellerId = await GetSellerIdAsync(userId, cancellationToken);
        return await GetDetailsAsync(sellerId, id, cancellationToken);
    }

    public async Task<SellerOrderDetailsDto> ChangeStatusAsync(
        int userId, int id, ChangeSellerOrderStatusRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sellerId = await GetSellerIdAsync(userId, cancellationToken);
        var to = ParseTarget(request.Status);

        await statusChanger.ChangeAsync(async ct =>
        {
            var subOrder = await unitOfWork.SellerOrders.GetOwnedForUpdateAsync(sellerId, id, ct)
                ?? throw new NotFoundException($"Order {id} was not found.");
            return [subOrder];
        }, to, OrderActor.Seller, request.Reason, userId, cancellationToken);

        return await GetDetailsAsync(sellerId, id, cancellationToken);
    }

    // ---------------------------------------------------------------------

    private async Task<SellerOrderDetailsDto> GetDetailsAsync(int sellerId, int id, CancellationToken cancellationToken)
    {
        var row = await unitOfWork.SellerOrders.GetSellerDetailsAsync(sellerId, id, cancellationToken)
            ?? throw new NotFoundException($"Order {id} was not found.");
        var customer = await customers.GetOneAsync(row.CustomerId, cancellationToken);
        return row.ToSellerDetailsDto(customer.FullName);
    }

    private async Task<int> GetSellerIdAsync(int userId, CancellationToken cancellationToken)
    {
        // The SellerCanFulfil policy has already checked the profile; this only maps user -> seller id.
        var profile = await unitOfWork.SellerProfiles.GetByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Seller profile was not found.");
        return profile.Id;
    }

    private static string NameOf(IReadOnlyDictionary<int, OrderCustomerDto> names, int customerId) =>
        names.TryGetValue(customerId, out var customer) ? customer.FullName : CustomerDirectory.Unknown(customerId).FullName;

    internal static SellerOrderStatus ParseTarget(string status) =>
        OrderRules.TryParseTargetStatus(status, out var to)
            ? to
            : throw new RequestValidationException("status", OrderRules.TargetStatusMessage);
}

public sealed class AdminOrderService(
    IUnitOfWork unitOfWork,
    SellerOrderStatusChanger statusChanger,
    CustomerDirectory customers) : IAdminOrderService
{
    public async Task<PagedResponse<OrderListItemDto>> GetOrdersAsync(AdminOrderListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var filter = new AdminOrderFilter
        {
            Status = OrderRules.TryParseStatus(query.Status, out var status) ? status.ToString() : null,
            OrderNumber = query.Q
        };

        var page = await unitOfWork.Orders.GetAdminPagedAsync(
            filter, query.Page, query.PageSize, OrderRules.IsNewestFirst(query.Sort), cancellationToken);

        var found = await customers.GetAsync(page.Items.Select(r => r.CustomerId), cancellationToken);
        return page.ToResponse(row => row.ToDto(
            found.TryGetValue(row.CustomerId, out var customer) ? customer : CustomerDirectory.Unknown(row.CustomerId)));
    }

    public async Task<OrderDto> GetOrderAsync(int id, CancellationToken cancellationToken = default)
    {
        var order = await unitOfWork.Orders.GetDetailsAsync(id, customerId: null, cancellationToken)
            ?? throw new NotFoundException($"Order {id} was not found.");
        var customer = await customers.GetOneAsync(order.CustomerId, cancellationToken);
        return order.ToDto(OrderActor.Admin, customer);
    }

    public async Task<OrderDto> ChangeStatusAsync(
        int adminUserId, int orderId, int sellerOrderId, ChangeSellerOrderStatusRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var to = SellerOrderService.ParseTarget(request.Status);

        await statusChanger.ChangeAsync(async ct =>
        {
            var order = await unitOfWork.Orders.GetForUpdateAsync(orderId, customerId: null, ct)
                ?? throw new NotFoundException($"Order {orderId} was not found.");
            var subOrder = order.SellerOrders.FirstOrDefault(s => s.Id == sellerOrderId)
                ?? throw new NotFoundException($"Part {sellerOrderId} of order {orderId} was not found.");
            return [subOrder];
        }, to, OrderActor.Admin, request.Reason, adminUserId, cancellationToken);

        return await GetOrderAsync(orderId, cancellationToken);
    }
}
