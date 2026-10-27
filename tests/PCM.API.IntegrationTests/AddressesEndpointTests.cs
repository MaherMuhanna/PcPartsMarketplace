using System.Net;
using System.Net.Http.Json;
using PCM.API.IntegrationTests.Infrastructure;
using PCM.Infrastructure.DTO.Addresses;
using PCM.Infrastructure.DTO.Common;

namespace PCM.API.IntegrationTests;

[Collection(nameof(ApiCollection))]
public sealed class AddressesEndpointTests(ApiFactory factory)
{
    private const string BasePath = "/api/v1/account/addresses";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task First_address_becomes_default_and_a_new_default_replaces_it()
    {
        var client = factory.CreateClient(await factory.RegisterCustomerAsync());

        var first = await CreateAsync(client, "Amman", isDefault: false);
        Assert.True(first.IsDefault, "The first address is always the default.");

        var second = await CreateAsync(client, "Irbid", isDefault: true);
        Assert.True(second.IsDefault);

        var list = await client.GetFromJsonAsync<PagedResponse<AddressDto>>(BasePath, Ct);
        Assert.Equal(2, list!.TotalCount);
        Assert.Single(list.Items, a => a.IsDefault);
        Assert.Equal(second.Id, list.Items[0].Id);   // default first
    }

    [Fact]
    public async Task Set_default_and_delete_keep_exactly_one_default()
    {
        var client = factory.CreateClient(await factory.RegisterCustomerAsync());
        var first = await CreateAsync(client, "Amman", isDefault: false);
        var second = await CreateAsync(client, "Zarqa", isDefault: false);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsync($"{BasePath}/{second.Id}/default", null, Ct)).StatusCode);
        Assert.True((await client.GetFromJsonAsync<AddressDto>($"{BasePath}/{second.Id}", Ct))!.IsDefault);
        Assert.False((await client.GetFromJsonAsync<AddressDto>($"{BasePath}/{first.Id}", Ct))!.IsDefault);

        // Deleting the default promotes the remaining address.
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"{BasePath}/{second.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{BasePath}/{second.Id}", Ct)).StatusCode);
        Assert.True((await client.GetFromJsonAsync<AddressDto>($"{BasePath}/{first.Id}", Ct))!.IsDefault);
    }

    [Fact]
    public async Task Update_changes_the_fields()
    {
        var client = factory.CreateClient(await factory.RegisterCustomerAsync());
        var address = await CreateAsync(client, "Amman", isDefault: false);

        var response = await client.PutAsJsonAsync($"{BasePath}/{address.Id}", new UpdateAddressRequest
        {
            City = "Aqaba",
            Street = "King Hussein Street",
            Details = null,
            Phone = "+962795555555"
        }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<AddressDto>(Ct))!;
        Assert.Equal("Aqaba", updated.City);
        Assert.Null(updated.Details);
    }

    [Fact]
    public async Task Invalid_phone_returns_400()
    {
        var client = factory.CreateClient(await factory.RegisterCustomerAsync());

        var response = await client.PostAsJsonAsync(BasePath, new CreateAddressRequest
        {
            City = "Amman",
            Street = "Rainbow Street",
            Phone = "abc"
        }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("\"phone\"", await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task Another_customers_address_is_not_found()
    {
        var owner = factory.CreateClient(await factory.RegisterCustomerAsync());
        var address = await CreateAsync(owner, "Amman", isDefault: false);

        var intruder = factory.CreateClient(await factory.RegisterCustomerAsync());

        // IDOR rule: 404 (not 403), so the other user cannot even tell the address exists.
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.GetAsync($"{BasePath}/{address.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.PutAsJsonAsync($"{BasePath}/{address.Id}", new UpdateAddressRequest
        {
            City = "Hacked",
            Street = "Hacked",
            Phone = "+962790000000"
        }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.DeleteAsync($"{BasePath}/{address.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.PutAsync($"{BasePath}/{address.Id}/default", null, Ct)).StatusCode);

        var intruderList = await intruder.GetFromJsonAsync<PagedResponse<AddressDto>>(BasePath, Ct);
        Assert.Equal(0, intruderList!.TotalCount);
    }

    [Fact]
    public async Task Sellers_cannot_use_the_address_book()
    {
        var seller = factory.CreateClient(await factory.RegisterSellerAsync());

        Assert.Equal(HttpStatusCode.Forbidden, (await seller.GetAsync(BasePath, Ct)).StatusCode);
    }

    [Fact]
    public async Task Anonymous_requests_are_rejected()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync(BasePath, Ct)).StatusCode);
    }

    private static async Task<AddressDto> CreateAsync(HttpClient client, string city, bool isDefault)
    {
        var response = await client.PostAsJsonAsync(BasePath, new CreateAddressRequest
        {
            City = city,
            Street = "Main Street 1",
            Details = "Floor 2",
            Phone = "+962791234567",
            IsDefault = isDefault
        }, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AddressDto>(Ct))!;
    }
}
