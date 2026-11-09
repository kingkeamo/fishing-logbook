using System.Net;
using AwesomeAssertions;

namespace FishingLogBook.Mobile.Tests.Features.Locations.Clients.LocationLookupClientTests;

public class WhenTestingGet : BaseLocationLookupClientTest
{
    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task ItShouldThrowWhenTheApiFails(HttpStatusCode status)
    {
        // Arrange
        var (sut, server) = CreateClient(_ => Json("{}", status));

        // Act
        var act = () => sut.GetAsync(53.3498, -6.2603, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        server.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task ItShouldReturnNoLabelWhenTheApiHasNoMatch()
    {
        // Arrange
        var (sut, server) = CreateClient(_ => Json(string.Empty, HttpStatusCode.NotFound));

        // Act
        var result = await sut.GetAsync(53.3498, -6.2603, CancellationToken.None);

        // Assert
        result.Should().BeNull();
        server.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task ItShouldReturnNoLabelWhenTheResponseCannotBeRead()
    {
        // Arrange
        var (sut, server) = CreateClient(_ => Json("not json"));

        // Act
        var result = await sut.GetAsync(53.3498, -6.2603, CancellationToken.None);

        // Assert
        result.Should().BeNull();
        server.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task ItShouldPostTheCoordinatesAndReadTheLabel()
    {
        // Arrange
        string? body = null;
        var (sut, server) = CreateClient(request =>
        {
            body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return Json("""{"components":["Dublin","Ireland"],"locality":"Dublin","country":"Ireland"}""");
        });

        // Act
        var result = await sut.GetAsync(53.3498, -6.2603, CancellationToken.None);

        // Assert
        result!.Components.Should().Equal("Dublin", "Ireland");
        result.Locality.Should().Be("Dublin");
        result.Country.Should().Be("Ireland");
        body.Should().Contain("53.3498").And.Contain("-6.2603");
        var request = server.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Post);
        request.Uri.Should().Be(new Uri("https://api.example.com/api/locations/label"));
        request.Authorization.Should().Be("Bearer " + AccessToken);
    }
}
