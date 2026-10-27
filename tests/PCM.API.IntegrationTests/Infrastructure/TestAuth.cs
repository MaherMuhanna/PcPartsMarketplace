using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using PCM.DomainEntities.Identity;
using PCM.Infrastructure.DTO.Auth;
using PCM.Infrastructure.Security;

namespace PCM.API.IntegrationTests.Infrastructure;

/// <summary>Helpers that create users through the real API and return their tokens.</summary>
public static class TestAuth
{
    public const string Password = "Test@12345";

    public static string NewEmail(string prefix = "user") => $"{prefix}-{Guid.NewGuid():N}@test.local";

    public static async Task<AuthResponse> RegisterCustomerAsync(this ApiFactory factory, string? email = null)
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/register/customer", new RegisterCustomerRequest
        {
            FullName = "Test Customer",
            Email = email ?? NewEmail("customer"),
            Password = Password
        }, TestContext.Current.CancellationToken);

        return await ReadAuthAsync(response);
    }

    public static async Task<AuthResponse> RegisterSellerAsync(this ApiFactory factory, string? storeName = null)
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/register/seller", new RegisterSellerRequest
        {
            FullName = "Test Seller",
            Email = NewEmail("seller"),
            Password = Password,
            StoreName = storeName ?? $"Store {Guid.NewGuid():N}"[..20],
            ContactPhone = "+962790000000"
        }, TestContext.Current.CancellationToken);

        return await ReadAuthAsync(response);
    }

    public static async Task<AuthResponse> LoginAsync(this ApiFactory factory, string email, string password = Password)
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest { Email = email, Password = password }, TestContext.Current.CancellationToken);

        return await ReadAuthAsync(response);
    }

    /// <summary>Admins cannot self-register, so the test creates one with UserManager and signs in through the API.</summary>
    public static async Task<AuthResponse> CreateAdminAsync(this ApiFactory factory)
    {
        var email = NewEmail("admin");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var admin = new ApplicationUser { UserName = email, Email = email, FullName = "Test Admin" };
            Assert.True((await userManager.CreateAsync(admin, Password)).Succeeded);
            Assert.True((await userManager.AddToRoleAsync(admin, Roles.Admin)).Succeeded);
        }

        return await factory.LoginAsync(email);
    }

    /// <summary>A client that sends the given access token as a Bearer header.</summary>
    public static HttpClient CreateClient(this ApiFactory factory, AuthResponse auth)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private static async Task<AuthResponse> ReadAuthAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccessStatusCode, $"Expected success but got {(int)response.StatusCode}: {body}");
        return (await response.Content.ReadFromJsonAsync<AuthResponse>(TestContext.Current.CancellationToken))!;
    }
}
