using System.Net;
using Xunit;

namespace BpTracker.Api.Tests;

public class AnalyzeIntegrationTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly CustomWebApplicationFactory<Program> _factory;

    public AnalyzeIntegrationTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Analyze_WhenNoCookie_Returns401()
    {
        // Arrange
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var content = new MultipartFormDataContent();

        // Act
        var response = await client.PostAsync("/api/v1/measurements/analyze", content);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
