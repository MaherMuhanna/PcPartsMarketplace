using PCM.DomainEntities.DBEntities;

namespace PCM.Infrastructure.Catalog;

/// <summary>
/// In-memory view of the whole category tree, built from one query
/// (<see cref="Repositories.IRepository.ICategoryRepository.GetAllAsync"/>).
/// Answers the tree questions services ask: is it a leaf, what is under it, what is above it.
/// </summary>
public sealed class CategoryTree
{
    private readonly Dictionary<int, Category> _byId;
    private readonly Dictionary<string, Category> _bySlug;
    private readonly ILookup<int?, Category> _children;

    public CategoryTree(IReadOnlyList<Category> categories)
    {
        ArgumentNullException.ThrowIfNull(categories);

        _byId = categories.ToDictionary(c => c.Id);
        _bySlug = categories.ToDictionary(c => c.Slug, StringComparer.OrdinalIgnoreCase);
        // Keeps the repository order (SortOrder, Name) inside each parent.
        _children = categories.ToLookup(c => c.ParentId);
    }

    public IEnumerable<Category> Roots => _children[null];

    public Category? Find(int id) => _byId.GetValueOrDefault(id);

    public Category? FindBySlug(string slug) => _bySlug.GetValueOrDefault(slug.Trim());

    public IEnumerable<Category> ChildrenOf(int id) => _children[id];

    public bool IsLeaf(int id) => !_children[id].Any();

    /// <summary>The category and every category below it.</summary>
    public IReadOnlyList<int> SelfAndDescendantIds(int id)
    {
        var result = new List<int>();
        var pending = new Stack<int>([id]);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            result.Add(current);
            foreach (var child in _children[current])
            {
                pending.Push(child.Id);
            }
        }

        return result;
    }

    /// <summary>Breadcrumb from the root down to the category (inclusive).</summary>
    public IReadOnlyList<Category> PathTo(int id)
    {
        var path = new List<Category>();
        var current = Find(id);

        // The visited guard protects against a (corrupted) cycle in ParentId.
        var visited = new HashSet<int>();
        while (current is not null && visited.Add(current.Id))
        {
            path.Add(current);
            current = current.ParentId is { } parentId ? Find(parentId) : null;
        }

        path.Reverse();
        return path;
    }
}
