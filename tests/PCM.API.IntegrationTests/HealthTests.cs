using System.Net;
using PCM.API.IntegrationTests.Infrastructure;

namespace PCM.API.IntegrationTests;

[Collection(nameof(ApiCollection))]
public sealed class HealthTests(ApiFactory factory)
{
    [Fact]
    public async Task Health_endpoint_reports_healthy_when_database_is_reachable()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}
