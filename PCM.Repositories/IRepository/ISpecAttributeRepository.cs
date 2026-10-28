using PCM.DomainEntities.DBEntities;

namespace PCM.Repositories.IRepository;

public interface ISpecAttributeRepository : IRepository<SpecAttribute>
{
    /// <summary>Read-only attributes of a category with their options, both ordered by SortOrder.</summary>
    Task<IReadOnlyList<SpecAttribute>> GetByCategoryAsync(int categoryId, CancellationToken cancellationToken = default);

    /// <summary>Tracked attribute with its options, or null.</summary>
    Task<SpecAttribute?> GetWithOptionsAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>True if any product stores a value for the attribute.</summary>
    Task<bool> IsInUseAsync(int attributeId, CancellationToken cancellationToken = default);

    /// <summary>True if any product uses the option.</summary>
    Task<bool> IsOptionInUseAsync(int optionId, CancellationToken cancellationToken = default);
}
