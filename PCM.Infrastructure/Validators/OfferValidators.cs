using FluentValidation;
using PCM.DomainEntities.Enums;
using PCM.Infrastructure.DTO.Offers;

namespace PCM.Infrastructure.Validators;

// Shape rules only. Rules that need the database (published product, duplicate offer,
// stock not below reserved) live in SellerOfferService and OfferStock.

public sealed class SellerOfferListQueryValidator : PageQueryValidator<SellerOfferListQuery>
{
    public SellerOfferListQueryValidator() : base(SellerOfferListQuery.AllowedSorts)
    {
        RuleFor(q => q.Q).MaximumLength(100);

        RuleFor(q => q.Condition)
            .Must(condition => condition is null || OfferRules.IsCondition(condition))
            .WithMessage(OfferRules.ConditionMessage);

        RuleFor(q => q.Stock)
            .Must(stock => stock is null || SellerOfferListQuery.StockFilters.Contains(stock, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Stock must be one of: {string.Join(", ", SellerOfferListQuery.StockFilters)}.");
    }
}

public sealed class StockMovementListQueryValidator() : PageQueryValidator<StockMovementListQuery>(StockMovementListQuery.AllowedSorts);

public sealed class CreateOfferRequestValidator : AbstractValidator<CreateOfferRequest>
{
    public CreateOfferRequestValidator()
    {
        RuleFor(r => r.ProductId).GreaterThan(0);

        RuleFor(r => r.Condition)
            .NotEmpty()
            .Must(OfferRules.IsCondition)
            .WithMessage(OfferRules.ConditionMessage);

        RuleFor(r => r.Price).ValidPrice();
        RuleFor(r => r.Stock).InclusiveBetween(0, OfferRules.MaxQuantity);
        RuleFor(r => r.LowStockThreshold).InclusiveBetween(0, OfferRules.MaxLowStockThreshold);
    }
}

public sealed class UpdateOfferRequestValidator : AbstractValidator<UpdateOfferRequest>
{
    public UpdateOfferRequestValidator()
    {
        RuleFor(r => r.Price).ValidPrice();
        RuleFor(r => r.LowStockThreshold).InclusiveBetween(0, OfferRules.MaxLowStockThreshold);
    }
}

public sealed class StockChangeRequestValidator : AbstractValidator<StockChangeRequest>
{
    public StockChangeRequestValidator()
    {
        RuleFor(r => r.Type)
            .NotEmpty()
            .Must(type => OfferRules.TryParseChangeType(type, out _))
            .WithMessage("Type must be Restock or Adjustment.");

        RuleFor(r => r.Change)
            .NotEqual(0)
            .InclusiveBetween(-OfferRules.MaxQuantity, OfferRules.MaxQuantity);

        // Restock = goods received, so it can only add. Removing stock is an Adjustment with a reason.
        RuleFor(r => r.Change)
            .GreaterThan(0)
            .When(r => OfferRules.TryParseChangeType(r.Type, out var type) && type == StockMovementReason.Restock)
            .WithMessage("A restock must add stock. Use an Adjustment to remove stock.");

        RuleFor(r => r.Note).MaximumLength(300);

        RuleFor(r => r.Note)
            .Must(note => note is not null && note.Trim().Length >= 3)
            .When(r => OfferRules.TryParseChangeType(r.Type, out var type) && type == StockMovementReason.Adjustment)
            .WithMessage("An adjustment needs a note of at least 3 characters (e.g. stock count, damaged item).");
    }
}

/// <summary>Limits and parsing shared by the offer validators and services.</summary>
public static class OfferRules
{
    public const decimal MinPrice = 0.01m;
    public const decimal MaxPrice = 999_999.99m;
    public const int MaxQuantity = 100_000;
    public const int MaxLowStockThreshold = 1_000;
    public const int DefaultLowStockThreshold = 5;

    public const string ConditionMessage = "Condition must be New or Used.";

    public static bool IsCondition(string? value) =>
        Enum.TryParse<OfferCondition>(value, ignoreCase: true, out var condition) && Enum.IsDefined(condition) && !IsNumeric(value);

    /// <summary>Restock or Adjustment (case-insensitive). Reserve / Release / Sale are written by orders, never by sellers.</summary>
    public static bool TryParseChangeType(string? value, out StockMovementReason type)
    {
        type = default;
        return !IsNumeric(value)
            && Enum.TryParse(value, ignoreCase: true, out type)
            && type is StockMovementReason.Restock or StockMovementReason.Adjustment;
    }

    public static IRuleBuilderOptions<T, decimal> ValidPrice<T>(this IRuleBuilder<T, decimal> rule) =>
        rule.InclusiveBetween(MinPrice, MaxPrice)
            .Must(price => decimal.Round(price, 2) == price)
            .WithMessage("Price can have at most 2 decimal places.");

    // Enum.TryParse also accepts numbers ("1"); only the names are valid input.
    private static bool IsNumeric(string? value) =>
        !string.IsNullOrEmpty(value) && (char.IsDigit(value[0]) || value[0] is '-' or '+');
}
