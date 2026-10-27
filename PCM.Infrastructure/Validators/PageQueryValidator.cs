using FluentValidation;
using PCM.Infrastructure.DTO.Brands;
using PCM.Infrastructure.DTO.Common;

namespace PCM.Infrastructure.Validators;

/// <summary>Rules shared by every paginated query.</summary>
public abstract class PageQueryValidator<TQuery> : AbstractValidator<TQuery> where TQuery : PageQuery
{
    protected PageQueryValidator(IReadOnlyCollection<string> allowedSorts)
    {
        RuleFor(q => q.Page)
            .GreaterThanOrEqualTo(1);

        RuleFor(q => q.PageSize)
            .InclusiveBetween(1, PageQuery.MaxPageSize);

        RuleFor(q => q.Sort)
            .Must(sort => sort is null || allowedSorts.Contains(sort, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Sort must be one of: {string.Join(", ", allowedSorts)}.");
    }
}

public sealed class BrandListQueryValidator() : PageQueryValidator<BrandListQuery>(BrandListQuery.AllowedSorts);
