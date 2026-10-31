using FluentValidation;
using PCM.DomainEntities.Enums;
using PCM.Infrastructure.DTO.Carts;
using PCM.Infrastructure.DTO.Orders;
using PCM.Infrastructure.Orders;

namespace PCM.Infrastructure.Validators;

// Shape rules only. Rules that need the database (offer purchasable, stock, prices, ownership,
// allowed status moves) live in the cart, checkout and order services.

/// <summary>Limits shared by the cart and checkout validators and services.</summary>
public static class CartRules
{
    /// <summary>Business rule 10: 1 to 10 units per cart line (also the CHECK on CartItem and OrderItem).</summary>
    public const int MinQuantity = 1;
    public const int MaxQuantity = 10;

    /// <summary>Distinct lines per cart (keeps a cart, and one checkout transaction, small).</summary>
    public const int MaxLines = 50;
}

public sealed class AddCartItemRequestValidator : AbstractValidator<AddCartItemRequest>
{
    public AddCartItemRequestValidator()
    {
        RuleFor(r => r.OfferId).GreaterThan(0);
        RuleFor(r => r.Quantity).InclusiveBetween(CartRules.MinQuantity, CartRules.MaxQuantity);
    }
}

public sealed class UpdateCartItemRequestValidator : AbstractValidator<UpdateCartItemRequest>
{
    public UpdateCartItemRequestValidator()
    {
        RuleFor(r => r.Quantity).InclusiveBetween(CartRules.MinQuantity, CartRules.MaxQuantity);
    }
}

public sealed class CheckoutRequestValidator : AbstractValidator<CheckoutRequest>
{
    public CheckoutRequestValidator()
    {
        RuleFor(r => r.AddressId).GreaterThan(0).WithMessage("Choose a shipping address.");

        RuleFor(r => r.Items)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .NotEmpty().WithMessage("Your cart is empty.")
            .Must(items => items.Count <= CartRules.MaxLines)
            .WithMessage($"A checkout can have at most {CartRules.MaxLines} lines.")
            .Must(items => items.Select(i => i.CartItemId).Distinct().Count() == items.Count)
            .WithMessage("Each cart line can appear only once.");

        RuleForEach(r => r.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.CartItemId).GreaterThan(0);
            item.RuleFor(i => i.UnitPrice).ValidPrice();
            item.RuleFor(i => i.Quantity).InclusiveBetween(CartRules.MinQuantity, CartRules.MaxQuantity);
        });
    }
}

public sealed class CancelOrderRequestValidator : AbstractValidator<CancelOrderRequest>
{
    public CancelOrderRequestValidator()
    {
        RuleFor(r => r.Reason).MaximumLength(SellerOrderWorkflow.MaxReasonLength);
    }
}

public sealed class ChangeSellerOrderStatusRequestValidator : AbstractValidator<ChangeSellerOrderStatusRequest>
{
    public ChangeSellerOrderStatusRequestValidator()
    {
        RuleFor(r => r.Status)
            .NotEmpty()
            .Must(status => OrderRules.TryParseTargetStatus(status, out _))
            .WithMessage(OrderRules.TargetStatusMessage);

        RuleFor(r => r.Reason).MaximumLength(SellerOrderWorkflow.MaxReasonLength);

        // A seller or admin cancelling must tell the customer why.
        RuleFor(r => r.Reason)
            .Must(reason => reason is not null && reason.Trim().Length >= 3)
            .When(r => OrderRules.TryParseTargetStatus(r.Status, out var status) && status == SellerOrderStatus.Cancelled)
            .WithMessage("Give a reason of at least 3 characters when cancelling.");
    }
}

public sealed class OrderListQueryValidator() : PageQueryValidator<OrderListQuery>(OrderListQuery.AllowedSorts);

public sealed class SellerOrderListQueryValidator : PageQueryValidator<SellerOrderListQuery>
{
    public SellerOrderListQueryValidator() : base(SellerOrderListQuery.AllowedSorts)
    {
        RuleFor(q => q.Status)
            .Must(status => status is null || OrderRules.TryParseStatus(status, out _))
            .WithMessage(OrderRules.StatusMessage);
    }
}

public sealed class AdminOrderListQueryValidator : PageQueryValidator<AdminOrderListQuery>
{
    public AdminOrderListQueryValidator() : base(AdminOrderListQuery.AllowedSorts)
    {
        RuleFor(q => q.Status)
            .Must(status => status is null || OrderRules.TryParseStatus(status, out _))
            .WithMessage(OrderRules.StatusMessage);

        RuleFor(q => q.Q).MaximumLength(20);
    }
}

/// <summary>Parsing shared by the order validators and services.</summary>
public static class OrderRules
{
    public const string StatusMessage = "Status must be Pending, Confirmed, Shipped, Delivered or Cancelled.";
    public const string TargetStatusMessage = "Status must be Confirmed, Shipped, Delivered or Cancelled.";

    /// <summary>Any sub-order status (case-insensitive names only, no numbers).</summary>
    public static bool TryParseStatus(string? value, out SellerOrderStatus status)
    {
        status = default;
        return !string.IsNullOrEmpty(value)
            && char.IsLetter(value[0])
            && Enum.TryParse(value, ignoreCase: true, out status)
            && Enum.IsDefined(status);
    }

    /// <summary>A status a sub-order can be moved to (every status except the initial Pending).</summary>
    public static bool TryParseTargetStatus(string? value, out SellerOrderStatus status) =>
        TryParseStatus(value, out status) && status != SellerOrderStatus.Pending;

    /// <summary>"newest" (default) or "oldest".</summary>
    public static bool IsNewestFirst(string? sort) =>
        !string.Equals(sort, "oldest", StringComparison.OrdinalIgnoreCase);
}
