using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace PCM.API.IntegrationTests.Infrastructure;

/// <summary>
/// Starts a throwaway SQL Server 2025 container, builds the database from
/// Database/schema.sql + Database/seed.sql, and hosts PCM.API in memory pointing at it.
/// Shared by all tests in <see cref="ApiCollection"/> (one container per test run).
/// </summary>
public sealed partial class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string DatabaseName = "PcPartsMarketplace";

    private readonly MsSqlContainer _sqlServer =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2025-latest").Build();

    /// <summary>Connection string to the PcPartsMarketplace database inside the container.</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        await _sqlServer.StartAsync();
        await RunScriptAsync(_sqlServer.GetConnectionString(), "schema.sql");
        await RunScriptAsync(_sqlServer.GetConnectionString(), "seed.sql");

        ConnectionString = new SqlConnectionStringBuilder(_sqlServer.GetConnectionString())
        {
            InitialCatalog = DatabaseName
        }.ConnectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:PcParts", ConnectionString);

        // Throwaway key for the in-memory API (never used outside tests).
        builder.UseSetting("Jwt:SigningKey", "integration-tests-only-signing-key-0123456789");

        // Every test shares one client "IP"; lift the per-IP auth limit so tests do not hit 429.
        builder.UseSetting("RateLimiting:AuthPermitPerMinute", "100000");

        // The auto-cancel job would race the tests; they call PendingOrderExpiry directly instead.
        builder.UseSetting("Orders:AutoCancel:Enabled", "false");
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _sqlServer.DisposeAsync();
    }

    /// <summary>
    /// Runs a script from Database/ the way sqlcmd/SSMS do: split into batches on "GO" lines,
    /// all on one connection so USE and SET statements carry over.
    /// </summary>
    private static async Task RunScriptAsync(string masterConnectionString, string fileName)
    {
        var scriptPath = Path.Combine(AppContext.BaseDirectory, "Database", fileName);
        var script = await File.ReadAllTextAsync(scriptPath);

        await using var connection = new SqlConnection(masterConnectionString);
        await connection.OpenAsync();

        foreach (var batch in GoSeparator().Split(script).Where(b => !string.IsNullOrWhiteSpace(b)))
        {
            await using var command = new SqlCommand(batch, connection) { CommandTimeout = 120 };
            await command.ExecuteNonQueryAsync();
        }
    }

    [GeneratedRegex(@"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex GoSeparator();
}

/// <summary>All API tests share one container and one in-memory API host.</summary>
[CollectionDefinition(nameof(ApiCollection))]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>;
