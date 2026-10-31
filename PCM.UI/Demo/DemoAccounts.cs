namespace PCM.UI.Demo;

/// <summary>One seeded demo user offered on the login page.</summary>
public sealed record DemoAccount(string Role, string Email, string Note);

/// <summary>
/// The demo accounts box on the login page. Shown only when <c>Seed:DemoUsers</c> is true AND the
/// environment is Development (docker-compose runs Development, so reviewers get it; production never does).
/// The accounts are the ones PCM.API seeds (DemoUserSeeder); the password is the shared demo password.
/// </summary>
public sealed class DemoAccounts(IConfiguration configuration, IWebHostEnvironment environment)
{
    private static readonly DemoAccount[] Accounts =
    [
        new("Admin", "admin@pcm.local", "Sellers, catalog, orders"),
        new("Seller", "seller1@pcm.local", "Tech Hub, approved"),
        new("Seller", "seller3@pcm.local", "Circuit Store, approved"),
        new("Seller", "seller2@pcm.local", "Byte Corner, pending review"),
        new("Customer", "customer@pcm.local", "Cart, orders, addresses")
    ];

    /// <summary>True when the box should be shown.</summary>
    public bool IsEnabled =>
        environment.IsDevelopment()
        && configuration.GetValue<bool>("Seed:DemoUsers")
        && !string.IsNullOrEmpty(Password);

    /// <summary>Shared password of the demo users (Development configuration only).</summary>
    public string? Password => configuration["Seed:DemoPassword"];

    /// <summary>The accounts, or an empty list when the box is disabled.</summary>
    public IReadOnlyList<DemoAccount> List => IsEnabled ? Accounts : [];
}
