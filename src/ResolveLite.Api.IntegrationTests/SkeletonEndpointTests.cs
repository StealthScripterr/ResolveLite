using System.Net;
using System.Net.Http.Json;

namespace ResolveLite.Api.IntegrationTests;

/// <summary>
/// The 6.2 skeleton tests, now running on ApiFactory so that /health/ready
/// does not try to reach real Azure Storage from a unit-test run.
/// </summary>
public class SkeletonEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Liveness_endpoint_returns_200_Healthy()
    {
        var response = await _client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Readiness_endpoint_returns_200()
    {
        var response = await _client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Info_endpoint_identifies_the_app()
    {
        var info = await _client.GetFromJsonAsync<AppInfo>("/api/info");

        Assert.NotNull(info);
        Assert.Equal("ResolveLite", info.Name);
    }
}
