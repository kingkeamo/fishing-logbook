using System.Net;
using AwesomeAssertions;

namespace FishingLogBook.Web.Tests.Features.Locations.Clients.LocationLookupClientTests;

public class WhenTestingGet : BaseLocationLookupClientTest
{
    [Fact]
    public async Task ItShouldReturnNoLabelForNoContent()
    {
        // Arrange
        var handler = new RecordingHandler(string.Empty, HttpStatusCode.NoContent);
        var client = CreateClient(handler);

        // Act
        var result = await client.GetAsync(53.3498, -6.2603, CancellationToken.None);

        // Assert
        result.Should().BeNull();
        handler.LastRequest!.RequestUri!.AbsoluteUri.Should().Be(
            "https://api.test/api/locations/label");
    }

    [Fact]
    public async Task ItShouldThrowWhenTheApiFails()
    {
        // Arrange
        var handler = new RecordingHandler("{}", HttpStatusCode.ServiceUnavailable);
        var client = CreateClient(handler);

        // Act
        var act = () => client.GetAsync(53.3498, -6.2603, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
    }

    [Fact]
    public async Task ItShouldReadTheLocationLabel()
    {
        // Arrange
        var handler = new RecordingHandler(
            """{"displayName":"Dublin, Ireland","locality":"Dublin","region":null,"country":"Ireland"}""");
        var client = CreateClient(handler);

        // Act
        var result = await client.GetAsync(53.3498, -6.2603, CancellationToken.None);

        // Assert
        result!.DisplayName.Should().Be("Dublin, Ireland");
        result.Locality.Should().Be("Dublin");
        result.Country.Should().Be("Ireland");
        handler.LastRequest!.RequestUri!.AbsoluteUri.Should().Be(
            "https://api.test/api/locations/label");
        handler.LastBody.Should().Be("""{"latitude":53.3498,"longitude":-6.2603}""");
    }
}
