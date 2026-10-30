using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using PCM.DomainEntities.DBEntities;
using PCM.Infrastructure.DTO.Offers;
using PCM.Infrastructure.Exceptions;
using PCM.Infrastructure.Services;
using PCM.Repositories;
using PCM.Repositories.IRepository;
using PCM.Repositories.Models;

namespace PCM.Infrastructure.Tests.Services;

public sealed class SellerOfferServiceTests
{
    private const int UserId = 7;
    private const int SellerId = 3;
    private const int OfferId = 11;

    private readonly IOfferRepository _offers = Substitute.For<IOfferRepository>();
    private readonly IProductRepository _products = Substitute.For<IProductRepository>();
    private readonly ISellerProfileRepository _sellers = Substitute.For<ISellerProfileRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly SellerOfferService _service;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public SellerOfferServiceTests()
    {
        _unitOfWork.Offers.Returns(_offers);
        _unitOfWork.Products.Returns(_products);
        _unitOfWork.SellerProfiles.Returns(_sellers);
        _sellers.GetByUserIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(new SellerProfile { Id = SellerId, UserId = UserId });
        _offers.GetSellerRowAsync(SellerId, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult<SellerOfferRow?>(
                new SellerOfferRow(call.ArgAt<int>(1), 1, "Part", "part", "P-1", null, true, "New", 10m, 5, 0, 5, DateTime.UtcNow, null)));
        _service = new SellerOfferService(_unitOfWork, NullLogger<SellerOfferService>.Instance);
    }

    private static StockChangeRequest Restock(int change) => new() { Type = "Restock", Change = change };

    [Fact]
    public async Task Concurrency_conflict_is_retried_once_with_fresh_data()
    {
        // Two loads return two different instances, as two database reads would.
        var stale = new Offer { Id = OfferId, SellerId = SellerId, Stock = 5 };
        var fresh = new Offer { Id = OfferId, SellerId = SellerId, Stock = 8 };
        var loads = new Queue<Offer>([stale, fresh]);
        _offers.GetOwnedAsync(SellerId, OfferId, Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult<Offer?>(loads.Dequeue()));

        // First save: another request changed the row (RowVersion mismatch). Second save succeeds.
        var saves = 0;
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(_ => ++saves == 1
            ? Task.FromException<int>(new DbUpdateConcurrencyException("Row changed"))
            : Task.FromResult(1));

        await _service.ChangeStockAsync(UserId, OfferId, Restock(2), Ct);

        _unitOfWork.Received(1).ClearChanges();
        await _unitOfWork.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
        Assert.Equal(10, fresh.Stock);                       // applied again on the fresh numbers
        Assert.Equal(10, Assert.Single(fresh.StockMovements).StockAfter);
    }

    [Fact]
    public async Task Second_concurrency_conflict_is_not_swallowed()
    {
        _offers.GetOwnedAsync(SellerId, OfferId, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<Offer?>(new Offer { Id = OfferId, SellerId = SellerId, Stock = 5 }));
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<int>(new DbUpdateConcurrencyException("Row changed")));

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => _service.ChangeStockAsync(UserId, OfferId, Restock(2), Ct));

        await _unitOfWork.Received(SellerOfferService.MaxSaveAttempts).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Other_sellers_offer_is_not_found()
    {
        _offers.GetOwnedAsync(SellerId, OfferId, Arg.Any<CancellationToken>()).Returns((Offer?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.ChangeStockAsync(UserId, OfferId, Restock(1), Ct));
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_on_unpublished_product_is_a_validation_error()
    {
        _products.IsPublishedAsync(42, Arg.Any<CancellationToken>()).Returns(false);

        var error = await Assert.ThrowsAsync<RequestValidationException>(() =>
            _service.CreateAsync(UserId, new CreateOfferRequest { ProductId = 42, Condition = "New", Price = 10m }, Ct));

        Assert.True(error.Errors.ContainsKey("productId"));
    }

    [Fact]
    public async Task Duplicate_offer_is_a_conflict()
    {
        _products.IsPublishedAsync(42, Arg.Any<CancellationToken>()).Returns(true);
        _offers.ExistsForSellerAsync(SellerId, 42, "Used", Arg.Any<CancellationToken>()).Returns(true);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.CreateAsync(UserId, new CreateOfferRequest { ProductId = 42, Condition = "used", Price = 10m }, Ct));
    }

    [Fact]
    public async Task Create_records_initial_stock_as_a_restock()
    {
        _products.IsPublishedAsync(42, Arg.Any<CancellationToken>()).Returns(true);

        await _service.CreateAsync(UserId, new CreateOfferRequest { ProductId = 42, Condition = "new", Price = 10m, Stock = 6 }, Ct);

        await _offers.Received(1).AddAsync(Arg.Is<Offer>(o =>
            o.SellerId == SellerId &&
            o.Condition == "New" &&
            o.Stock == 6 &&
            o.LowStockThreshold == 5 &&
            o.StockMovements.Single().Reason == "Restock" &&
            o.StockMovements.Single().Change == 6), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_without_stock_writes_no_movement()
    {
        _products.IsPublishedAsync(42, Arg.Any<CancellationToken>()).Returns(true);

        await _service.CreateAsync(UserId, new CreateOfferRequest { ProductId = 42, Condition = "New", Price = 10m, Stock = 0, LowStockThreshold = 2 }, Ct);

        await _offers.Received(1).AddAsync(Arg.Is<Offer>(o => o.Stock == 0 && o.LowStockThreshold == 2 && o.StockMovements.Count == 0), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_with_reserved_stock_is_a_conflict()
    {
        _offers.GetOwnedAsync(SellerId, OfferId, Arg.Any<CancellationToken>())
            .Returns(new Offer { Id = OfferId, SellerId = SellerId, Stock = 5, ReservedStock = 1 });

        await Assert.ThrowsAsync<ConflictException>(() => _service.DeleteAsync(UserId, OfferId, Ct));
        _offers.DidNotReceive().Remove(Arg.Any<Offer>());
    }

    [Theory]
    [InlineData("in", OfferStockFilter.InStock)]
    [InlineData("LOW", OfferStockFilter.Low)]
    [InlineData("out", OfferStockFilter.OutOfStock)]
    [InlineData(null, OfferStockFilter.All)]
    public void Stock_filter_parsing(string? value, OfferStockFilter expected)
    {
        Assert.Equal(expected, SellerOfferService.ParseStockFilter(value));
    }
}
