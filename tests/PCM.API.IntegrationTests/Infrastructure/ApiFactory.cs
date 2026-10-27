using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace PCM.API.IntegrationTests.Infrastructure;

/// <summary>
/// Starts a throwaway SQL Server 2025 container, builds the database from
/// Database/schema.sql, and hosts PCM.API in memory pointing at it.
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
        await ApplySchemaAsync(_sqlServer.GetConnectionString());

        ConnectionString = new SqlConnectionStringBuilder(_sqlServer.GetConnectionString())
        {
            InitialCatalog = DatabaseName
        }.ConnectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:PcParts", ConnectionString);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _sqlServer.DisposeAsync();
    }

    /// <summary>
    /// Runs schema.sql the way sqlcmd/SSMS do: split into batches on "GO" lines,
    /// all on one connection so USE and SET statements carry over.
    /// </summary>
    private static async Task ApplySchemaAsync(string masterConnectionString)
    {
        var scriptPath = Path.Combine(AppContext.BaseDirectory, "Database", "schema.sql");
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
