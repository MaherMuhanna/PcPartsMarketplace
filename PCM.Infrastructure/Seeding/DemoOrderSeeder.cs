using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using PCM.DomainEntities.Enums;
using PCM.DomainEntities.Identity;
using PCM.Infrastructure.DTO.Carts;
using PCM.Infrastructure.DTO.Orders;
using PCM.Infrastructure.Services;
using PCM.Repositories;

namespace PCM.Infrastructure.Seeding;

/// <summary>
/// Demo orders (DEVELOPMENT ONLY, runs after <see cref="DemoOfferSeeder"/>): customer@pcm.local
/// places five orders through the real cart and checkout, then the sellers and the customer move
/// them through the flow, so every status tab has something in it:
/// one order split between Tech Hub and Circuit Store (Pending), one Delivered, one Shipped,
/// one Confirmed and one cancelled by the customer. Two items are left in the cart.
/// Stock is reserved, sold and released through the same services as real orders.
/// Idempotent: skipped when the customer already has an order.
/// </summary>
public sealed class DemoOrderSeeder(
    UserManager<ApplicationUser> userManager,
    IUnitOfWork unitOfWork,
    ICartService cartService,
    ICheckoutService checkoutService,
    ISellerOrderService sellerOrderService,
    ICustomerOrderService customerOrderService,
    ILogger<DemoOrderSeeder> logger)
{
    private const string CustomerEmail = "customer@pcm.local";
    private const string TechHub = "seller1@pcm.local";
    private const string CircuitStore = "seller3@pcm.local";

    /// <summary>One cart line: which seller's offer for which product.</summary>
    private sealed record DemoLine(string SellerEmail, string ProductSlug, string Condition, int Quantity);

    /// <summary>One order and the status it should end in.</summary>
    private sealed record DemoOrder(DemoLine[] Lines, SellerOrderStatus FinalStatus);

    private static readonly DemoOrder[] Orders =
    [
        new([new(TechHub, "corsair-vengeance-ddr5-32gb-2x16gb-6000mt-s-cl30", "New", 2)], SellerOrderStatus.Delivered),
        new([new(CircuitStore, "corsair-rm850e-850w-80-gold", "New", 1)], SellerOrderStatus.Shipped),
        new([new(TechHub, "sapphire-pulse-radeon-rx-7600-8gb", "New", 1)], SellerOrderStatus.Cancelled),
        new([new(TechHub, "amd-ryzen-7-7800x3d", "New", 1)], SellerOrderStatus.Confirmed),
        new([
            new(TechHub, "samsung-990-pro-2tb-nvme-ssd", "New", 1),
            new(CircuitStore, "wd-black-sn850x-1tb-nvme-ssd", "New", 2)
        ], SellerOrderStatus.Pending)
    ];

    private static readonly DemoLine[] CartLeftovers =
    [
        new(TechHub, "intel-core-i5-14600kf", "New", 1),
        new(CircuitStore, "kingston-fury-beast-16gb-2x8gb-ddr4-3200mt-s-cl16", "Used", 1)
    ];

    /// <summary>Seller steps to reach each final status.</summary>
    private static readonly Dictionary<SellerOrderStatus, SellerOrderStatus[]> Steps = new()
    {
        [SellerOrderStatus.Pending] = [],
        [SellerOrderStatus.Confirmed] = [SellerOrderStatus.Confirmed],
        [SellerOrderStatus.Shipped] = [SellerOrderStatus.Confirmed, SellerOrderStatus.Shipped],
        [SellerOrderStatus.Delivered] = [SellerOrderStatus.Confirmed, SellerOrderStatus.Shipped, SellerOrderStatus.Delivered],
        [SellerOrderStatus.Cancelled] = []
    };

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var customer = await userManager.FindByEmailAsync(CustomerEmail);
        if (customer is null)
        {
            logger.LogInformation("Demo orders skipped: {Email} does not exist", CustomerEmail);
            return;
        }

        if (await unitOfWork.Orders.ExistsAsync(o => o.CustomerId == customer.Id, cancellationToken))
        {
            return;
        }

        var address = await unitOfWork.Addresses.GetDefaultAsync(customer.Id, cancellationToken);
        var sellers = await LoadSellersAsync(cancellationToken);
        if (address is null || sellers.Count < 2)
        {
            logger.LogWarning("Demo orders skipped: the demo customer address or the demo sellers are missing");
            return;
        }

        // Start from an empty cart so each checkout contains exactly one demo order.
        await cartService.ClearAsync(customer.Id, cancellationToken);

        var placed = 0;
        foreach (var demo in Orders)
        {
            if (!await FillCartAsync(customer.Id, demo.Lines, sellers, cancellationToken))
            {
                await cartService.ClearAsync(customer.Id, cancellationToken);
                continue;
            }

            var order = await CheckoutAsync(customer.Id, address.Id, cancellationToken);
            await MoveAsync(customer.Id, order, demo.FinalStatus, sellers, cancellationToken);
            placed++;
        }

        await FillCartAsync(customer.Id, CartLeftovers, sellers, cancellationToken);
        logger.LogInformation("Demo orders created for {Email}: {Count}", CustomerEmail, placed);
    }

    // ---------------------------------------------------------------------

    /// <summary>Seller email to (user id, seller profile id).</summary>
    private async Task<Dictionary<string, (int UserId, int SellerId)>> LoadSellersAsync(CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, (int, int)>();
        foreach (var email in new[] { TechHub, CircuitStore })
        {
            var user = await userManager.FindByEmailAsync(email);
            var profile = user is null ? null : await unitOfWork.SellerProfiles.GetByUserIdAsync(user.Id, cancellationToken);
            if (user is not null && profile is not null)
            {
                result[email] = (user.Id, profile.Id);
            }
        }

        return result;
    }

    /// <summary>Adds the lines through the cart service; false when a demo offer is missing (e.g. edited by hand).</summary>
    private async Task<bool> FillCartAsync(
        int customerId, IReadOnlyList<DemoLine> lines, IReadOnlyDictionary<string, (int UserId, int SellerId)> sellers, CancellationToken cancellationToken)
    {
        var productIds = await unitOfWork.Products.GetPublishedIdsBySlugsAsync(lines.Select(l => l.ProductSlug).ToList(), cancellationToken);

        foreach (var line in lines)
        {
            var offerId = productIds.TryGetValue(line.ProductSlug, out var productId) && sellers.TryGetValue(line.SellerEmail, out var seller)
                ? await unitOfWork.Offers.FindIdAsync(seller.SellerId, productId, line.Condition, cancellationToken)
                : null;

            if (offerId is null)
            {
                logger.LogWarning("Demo order line skipped: no {Condition} offer of {Seller} for {Slug}", line.Condition, line.SellerEmail, line.ProductSlug);
                return false;
            }

            await cartService.AddItemAsync(customerId, new AddCartItemRequest { OfferId = offerId.Value, Quantity = line.Quantity }, cancellationToken);
        }

        return true;
    }

    /// <summary>Checks out the cart exactly as the checkout page would: the lines with the prices shown.</summary>
    private async Task<OrderDto> CheckoutAsync(int customerId, int addressId, CancellationToken cancellationToken)
    {
        var cart = await cartService.GetCartAsync(customerId, cancellationToken);
        return await checkoutService.CheckoutAsync(customerId, new CheckoutRequest
        {
            AddressId = addressId,
            Items = cart.Sellers
                .SelectMany(s => s.Items)
                .Select(i => new CheckoutItemRequest { CartItemId = i.Id, UnitPrice = i.UnitPrice, Quantity = i.Quantity })
                .ToList()
        }, cancellationToken);
    }

    private async Task MoveAsync(
        int customerId, OrderDto order, SellerOrderStatus finalStatus,
        IReadOnlyDictionary<string, (int UserId, int SellerId)> sellers, CancellationToken cancellationToken)
    {
        if (finalStatus == SellerOrderStatus.Cancelled)
        {
            await customerOrderService.CancelOrderAsync(customerId, order.Id,
                new CancelOrderRequest { Reason = "Found a better deal elsewhere" }, cancellationToken);
            return;
        }

        foreach (var subOrder in order.SellerOrders)
        {
            var sellerUserId = sellers.Values.First(s => s.SellerId == subOrder.SellerId).UserId;
            foreach (var step in Steps[finalStatus])
            {
                await sellerOrderService.ChangeStatusAsync(sellerUserId, subOrder.Id,
                    new ChangeSellerOrderStatusRequest { Status = step.ToString() }, cancellationToken);
            }
        }
    }
}
