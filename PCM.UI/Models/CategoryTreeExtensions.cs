using PCM.Infrastructure.DTO.Catalog;

namespace PCM.UI.Models;

/// <summary>A category with its position in the tree, for drop-downs and breadcrumbs.</summary>
/// <param name="Node">The category.</param>
/// <param name="Path">Names from the top level down to this category, e.g. ["Components", "RAM"].</param>
public sealed record CategoryEntry(CategoryTreeDto Node, IReadOnlyList<string> Path)
{
    /// <summary>"Components > RAM".</summary>
    public string PathText => string.Join(" > ", Path);

    public int Depth => Path.Count - 1;
}

public static class CategoryTreeExtensions
{
    /// <summary>Every category, parents before children, in the tree's sort order.</summary>
    public static IEnumerable<CategoryEntry> Flatten(this IEnumerable<CategoryTreeDto> roots) => Walk(roots, []);

    /// <summary>The category with this id, with its path, or null.</summary>
    public static CategoryEntry? Find(this IEnumerable<CategoryTreeDto> roots, int id) =>
        roots.Flatten().FirstOrDefault(entry => entry.Node.Id == id);

    /// <summary>Leaf categories, the only ones products can be added to.</summary>
    public static IReadOnlyList<CategoryEntry> Leaves(this IEnumerable<CategoryTreeDto> roots) =>
        [.. roots.Flatten().Where(entry => entry.Node.IsLeaf)];

    private static IEnumerable<CategoryEntry> Walk(IEnumerable<CategoryTreeDto> nodes, IReadOnlyList<string> parentPath)
    {
        foreach (var node in nodes.OrderBy(n => n.SortOrder).ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase))
        {
            IReadOnlyList<string> path = [.. parentPath, node.Name];
            yield return new CategoryEntry(node, path);

            foreach (var child in Walk(node.Children, path))
            {
                yield return child;
            }
        }
    }
}
