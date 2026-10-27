using System.Linq.Expressions;
using NSubstitute;
using PCM.DomainEntities.DBEntities;
using PCM.Infrastructure.DTO.Addresses;
using PCM.Infrastructure.Exceptions;
using PCM.Infrastructure.Services;
using PCM.Repositories;
using PCM.Repositories.IRepository;

namespace PCM.Infrastructure.Tests.Services;

public sealed class AddressServiceTests
{
    private const int UserId = 7;

    private readonly IAddressRepository _addresses = Substitute.For<IAddressRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly AddressService _service;

    public AddressServiceTests()
    {
        _unitOfWork.Addresses.Returns(_addresses);
        _unitOfWork.BeginTransactionAsync(Arg.Any<CancellationToken>()).Returns(Substitute.For<IUnitOfWorkTransaction>());
        _service = new AddressService(_unitOfWork);
    }

    private static CreateAddressRequest Request(bool isDefault) => new()
    {
        City = " Amman ",
        Street = "Main Street 1",
        Phone = "+962790000000",
        IsDefault = isDefault
    };

    [Fact]
    public async Task First_address_is_default_even_when_not_requested()
    {
        _addresses.ExistsAsync(Arg.Any<Expression<Func<Address, bool>>>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await _service.CreateAddressAsync(UserId, Request(isDefault: false), TestContext.Current.CancellationToken);

        Assert.True(result.IsDefault);
        Assert.Equal("Amman", result.City);   // trimmed
        await _addresses.Received(1).AddAsync(Arg.Is<Address>(a => a.UserId == UserId && a.IsDefault), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task New_default_clears_the_old_default_first()
    {
        var oldDefault = new Address { Id = 1, UserId = UserId, IsDefault = true, City = "Irbid", Street = "x", Phone = "0790000000" };
        _addresses.ExistsAsync(Arg.Any<Expression<Func<Address, bool>>>(), Arg.Any<CancellationToken>()).Returns(true);
        _addresses.GetDefaultAsync(UserId, Arg.Any<CancellationToken>()).Returns(oldDefault);

        var result = await _service.CreateAddressAsync(UserId, Request(isDefault: true), TestContext.Current.CancellationToken);

        Assert.True(result.IsDefault);
        Assert.False(oldDefault.IsDefault);
        // One save to clear the old default, one to insert the new address (unique index safety).
        await _unitOfWork.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Address_of_another_user_is_not_found()
    {
        _addresses.GetOwnedAsync(42, UserId, Arg.Any<CancellationToken>()).Returns((Address?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetAddressAsync(UserId, 42, TestContext.Current.CancellationToken));
    }
}
