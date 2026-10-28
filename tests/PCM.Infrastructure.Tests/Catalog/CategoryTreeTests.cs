using PCM.DomainEntities.DBEntities;
using PCM.Infrastructure.Catalog;

namespace PCM.Infrastructure.Tests.Catalog;

public sealed class CategoryTreeTests
{
    //  Components (1)
    //   |- CPU (2)
    //   '- Storage (3)
    //       '- SSD (4)
    private static readonly CategoryTree Tree = new(
    [
        new Category { Id = 1, Name = "Components", Slug = "components" },
        new Category { Id = 2, ParentId = 1, Name = "CPU", Slug = "cpu" },
        new Category { Id = 3, ParentId = 1, Name = "Storage", Slug = "storage" },
        new Category { Id = 4, ParentId = 3, Name = "SSD", Slug = "ssd" }
    ]);

    [Fact]
    public void IsLeaf_is_true_only_for_categories_without_children()
    {
        Assert.False(Tree.IsLeaf(1));
        Assert.True(Tree.IsLeaf(2));
        Assert.False(Tree.IsLeaf(3));
        Assert.True(Tree.IsLeaf(4));
    }

    [Fact]
    public void SelfAndDescendantIds_walks_every_level()
    {
        Assert.Equal([1, 2, 3, 4], Tree.SelfAndDescendantIds(1).Order());
        Assert.Equal([3, 4], Tree.SelfAndDescendantIds(3).Order());
        Assert.Equal([2], Tree.SelfAndDescendantIds(2));
    }

    [Fact]
    public void PathTo_goes_from_the_root_down()
    {
        Assert.Equal(["components", "storage", "ssd"], Tree.PathTo(4).Select(c => c.Slug));
    }

    [Fact]
    public void FindBySlug_ignores_case_and_returns_null_when_unknown()
    {
        Assert.Equal(2, Tree.FindBySlug("CPU")!.Id);
        Assert.Null(Tree.FindBySlug("gpu"));
    }
}
