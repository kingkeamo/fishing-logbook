using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Web.Tests.Features.Profile.Clients.ProfileClientTests;

public class WhenTestingFindAnglers : BaseProfileClientTest
{
    [Fact]
    public async Task ItShouldReturnNoAnglersWhenTheLookupIsNotSuccessful()
    {
        // Arrange
        var apiHandler = new RecordingHandler("""{"title":"error"}""", HttpStatusCode.ServiceUnavailable);
        var client = CreateClient(apiHandler);

        // Act
        var result = await client.FindAnglersAsync("mary", CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
        apiHandler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        apiHandler.LastRequest.RequestUri!.PathAndQuery.Should().Be("/api/profiles/lookup?q=mary");
    }

    [Fact]
    public async Task ItShouldReturnNoAnglersWhenTheResponseBodyIsNull()
    {
        // Arrange
        var apiHandler = new RecordingHandler("null");
        var client = CreateClient(apiHandler);

        // Act
        var result = await client.FindAnglersAsync("mary", CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
        apiHandler.LastRequest.Should().NotBeNull();
    }

    [Fact]
    public async Task ItShouldEscapeTheQueryAndReturnTheMatchingAnglers()
    {
        // Arrange
        var angler = new AnglerSummaryDto(Guid.NewGuid(), "Mary O'Neill", null, "Galway", "mary@example.test");
        var json = JsonSerializer.Serialize(new[] { angler }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var apiHandler = new RecordingHandler(json);
        var client = CreateClient(apiHandler);

        // Act
        var result = await client.FindAnglersAsync("mary o'neill & co", CancellationToken.None);

        // Assert
        result.Should().BeEquivalentTo([angler]);
        apiHandler.LastRequest!.RequestUri!.PathAndQuery
            .Should().Be($"/api/profiles/lookup?q={Uri.EscapeDataString("mary o'neill & co")}");
    }
}
