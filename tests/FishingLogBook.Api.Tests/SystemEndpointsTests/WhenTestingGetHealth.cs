using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Api.Tests.SystemEndpointsTests;

public class WhenTestingGetHealth : BaseSystemEndpointsTest
{
    public WhenTestingGetHealth(SystemApiFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task ItShouldReturnHealthy()
    {
        // Arrange
        var client = Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");
        var body = await response.Content.ReadFromJsonAsync<HealthDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body!.Status.Should().Be("Healthy");
    }
}
