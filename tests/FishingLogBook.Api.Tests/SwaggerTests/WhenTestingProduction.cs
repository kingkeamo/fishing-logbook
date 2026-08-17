using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FishingLogBook.Api.Tests.TestSupport;
using FishingLogBook.Domain.Enums;

namespace FishingLogBook.Api.Tests.SwaggerTests;

public class WhenTestingProduction : IClassFixture<SystemApiFactory>
{
    private readonly SystemApiFactory _factory;

    public WhenTestingProduction(SystemApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ItShouldServeTheOpenApiDocument()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/openapi/v1.json");
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("FishingLogBook");
        body.Should().NotContain("__test");
        body.Should().NotContain("platform-capabilities");
    }

    [Fact]
    public async Task ItShouldNotExposeAPublicGrantEndpoint()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var publicGrant = await client.PostAsJsonAsync(
            "/api/platform-capabilities/grant",
            new { targetUserId = Guid.NewGuid(), capability = PlatformCapabilityEnum.Guide });
        var testGrant = await client.PostAsJsonAsync(
            TestGrantPlatformCapabilityStartupFilter.Path,
            new { targetUserId = Guid.NewGuid(), capability = PlatformCapabilityEnum.Guide });

        // Assert
        publicGrant.StatusCode.Should().Be(HttpStatusCode.NotFound);
        testGrant.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ItShouldServeSwaggerUi()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/swagger/index.html");
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("swagger");
    }
}
