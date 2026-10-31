using PCM.DomainEntities.DBEntities;
using PCM.Infrastructure.DTO.Carts;
using PCM.Infrastructure.DTO.Orders;
using PCM.Infrastructure.Mappings;
using PCM.Infrastructure.Orders;
using PCM.Repositories.Models;

namespace PCM.Infrastructure.Tests.Orders;

/// <summary>Business rule 1: checkout compares every line with what the customer saw.</summary>
public sealed class CheckoutRulesTests
{
    private static Offer NewOffer(int id, decimal price = 50m, int stock = 10, int reserved = 0, string sellerStatus = "Approved") => new()
    {
        Id = id,
        Price = price,
        Stock = stock,
        ReservedStock = reserved,
        Seller = new SellerProfile { Status = sellerStatus },
        Product = new Product { Name = $"Part {id}", Status = "Approved", IsActive = true }
    };

    private static CartItem Line(int id, int offerId, int quantity) => new() { Id = id, OfferId = offerId, Quantity = quantity };

    private static CheckoutItemRequest Seen(int cartItemId, decimal price, int quantity) =>
        new() { CartItemId = cartItemId, UnitPrice = price, Quantity = quantity };

    [Fact]
    public void Unchanged_cart_has_no_issues()
    {
        var offers = new Dictionary<int, Offer> { [1] = NewOffer(1), [2] = NewOffer(2, price: 20m) };

        var issues = CheckoutRules.FindIssues([Line(10, 1, 2), Line(11, 2, 1)], offers, [Seen(10, 50m, 2), Seen(11, 20m, 1)]);

        Assert.Empty(issues);
    }

    [Fact]
    public void Each_kind_of_change_is_reported_with_its_numbers()
    {
        var offers = new Dictionary<int, Offer>
        {
            [1] = NewOffer(1, price: 55m),                        // price went up
            [2] = NewOffer(2, stock: 3, reserved: 2),             // only 1 left
            [3] = NewOffer(3, sellerStatus: "Suspended"),         // seller suspended
            [4] = NewOffer(4)                                     // quantity changed in another tab
        };

        var issues = CheckoutRules.FindIssues(
            [Line(10, 1, 1), Line(11, 2, 2), Line(12, 3, 1), Line(13, 4, 3)],
            offers,
            [Seen(10, 50m, 1), Seen(11, 50m, 2), Seen(12, 50m, 1), Seen(13, 50m, 1)]);

        Assert.Equal(
            new[] { (10, CheckoutProblems.PriceChanged), (11, CheckoutProblems.InsufficientStock), (12, CheckoutProblems.Unavailable), (13, CheckoutProblems.CartChanged) },
            issues.Select(i => (i.CartItemId, i.Problem)));

        var price = issues[0];
        Assert.Equal(((decimal?)50m, (decimal?)55m), (price.ExpectedPrice, price.CurrentPrice));
        Assert.Equal(1, issues[1].Available);
    }

    [Fact]
    public void Lines_added_or_removed_since_the_page_opened_are_reported()
    {
        var offers = new Dictionary<int, Offer> { [1] = NewOffer(1), [2] = NewOffer(2) };

        var issues = CheckoutRules.FindIssues(
            [Line(10, 1, 1), Line(12, 2, 1)],         // 12 is new, 11 was removed
            offers,
            [Seen(10, 50m, 1), Seen(11, 50m, 1)]);

        Assert.Equal(new[] { 12, 11 }, issues.Select(i => i.CartItemId));
        Assert.All(issues, i => Assert.Equal(CheckoutProblems.CartChanged, i.Problem));
    }

    [Fact]
    public void Deleted_or_missing_offer_is_unavailable()
    {
        var deleted = NewOffer(1);
        deleted.IsDeleted = true;

        var issues = CheckoutRules.FindIssues(
            [Line(10, 1, 1), Line(11, 99, 1)],
            new Dictionary<int, Offer> { [1] = deleted },
            [Seen(10, 50m, 1), Seen(11, 50m, 1)]);

        Assert.All(issues, i => Assert.Equal(CheckoutProblems.Unavailable, i.Problem));
        Assert.Equal(2, issues.Count);
    }
}

public sealed class CartMappingTests
{
    private static CartLineRow Row(int id, int sellerId, decimal price, int quantity, int stock, int reserved = 0, bool purchasable = true) =>
        new(id, id, id, $"Part {id}", $"part-{id}", null, sellerId, $"Store {sellerId}", "New", price, quantity, stock, reserved, purchasable, DateTime.UtcNow);

    [Fact]
    public void Lines_are_grouped_by_seller_with_totals()
    {
        var cart = new List<CartLineRow> { Row(1, 7, 10m, 2, 5), Row(2, 8, 3.5m, 1, 5), Row(3, 7, 1m, 3, 5) }.ToCartDto();

        Assert.Equal(new[] { 7, 8 }, cart.Sellers.Select(s => s.SellerId));
        Assert.Equal(23m, cart.Sellers[0].Subtotal);
        Assert.Equal((6, 26.5m, true), (cart.ItemCount, cart.Total, cart.CanCheckout));
    }

    [Fact]
    public void Problem_lines_block_checkout()
    {
        var cart = new List<CartLineRow>
        {
            Row(1, 7, 10m, 3, stock: 4, reserved: 2),     // 2 available, 3 wanted
            Row(2, 7, 10m, 1, stock: 9, purchasable: false)
        }.ToCartDto();

        var items = cart.Sellers.Single().Items;
        Assert.Equal((CartIssues.InsufficientStock, 2), (items[0].Issue, items[0].Available));
        Assert.Equal((CartIssues.Unavailable, 0), (items[1].Issue, items[1].Available));
        Assert.False(cart.CanCheckout);
    }

    [Fact]
    public void Empty_cart_cannot_check_out()
    {
        var cart = new List<CartLineRow>().ToCartDto();

        Assert.Equal((0, 0m, false), (cart.ItemCount, cart.Total, cart.CanCheckout));
    }
}
