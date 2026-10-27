using PCM.Infrastructure.DTO.Auth;

namespace PCM.UI.ApiClients;

public interface IAuthApiClient
{
    Task<AuthResponse> RegisterCustomerAsync(RegisterCustomerRequest request, CancellationToken cancellationToken = default);

    Task<AuthResponse> RegisterSellerAsync(RegisterSellerRequest request, CancellationToken cancellationToken = default);

    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);
}

/// <summary>Typed client for /api/v1/auth. Token refresh is done by ApiTokenHandler, not here.</summary>
public sealed class AuthApiClient(HttpClient httpClient) : ApiClientBase(httpClient), IAuthApiClient
{
    public Task<AuthResponse> RegisterCustomerAsync(RegisterCustomerRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<AuthResponse>(HttpMethod.Post, "api/v1/auth/register/customer", request, cancellationToken);

    public Task<AuthResponse> RegisterSellerAsync(RegisterSellerRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<AuthResponse>(HttpMethod.Post, "api/v1/auth/register/seller", request, cancellationToken);

    public Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<AuthResponse>(HttpMethod.Post, "api/v1/auth/login", request, cancellationToken);

    public Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, "api/v1/auth/logout", new RefreshTokenRequest { RefreshToken = refreshToken }, cancellationToken);
}
