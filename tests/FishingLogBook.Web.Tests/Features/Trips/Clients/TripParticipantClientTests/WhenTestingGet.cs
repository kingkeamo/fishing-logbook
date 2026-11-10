using System.Net;
using AwesomeAssertions;

namespace FishingLogBook.Web.Tests.Features.Trips.Clients.TripParticipantClientTests;

public class WhenTestingGet : BaseTripParticipantClientTest
{
    [Fact]
    public async Task ItShouldThrowWhenTheApiFails()
    {
        // Arrange
        var handler = new RecordingHandler(HttpStatusCode.ServiceUnavailable);
        var client = CreateClient(handler);

        // Act
        var act = () => client.GetAsync(TripId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
        handler.Calls.Should().Be(1);
    }

    [Fact]
    public async Task ItShouldReturnNothingWhenTheTripIsNotFound()
    {
        // Arrange
        var handler = new RecordingHandler(HttpStatusCode.NotFound);
        var client = CreateClient(handler);

        // Act
        var result = await client.GetAsync(TripId, CancellationToken.None);

        // Assert
        result.Should().BeNull();
        handler.LastRequest!.RequestUri!.AbsoluteUri.Should().Be($"https://api.test/api/trips/{TripId:D}/participants");
    }

    [Fact]
    public async Task ItShouldReadTheParticipants()
    {
        // Arrange
        var handler = new RecordingHandler(HttpStatusCode.OK, $$"""{"tripId":"{{TripId}}","role":"Owner"}""");
        var client = CreateClient(handler);

        // Act
        var result = await client.GetAsync(TripId, CancellationToken.None);

        // Assert
        result!.TripId.Should().Be(TripId);
        result.Role.Should().Be("Owner");
        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.AbsoluteUri.Should().Be($"https://api.test/api/trips/{TripId:D}/participants");
    }
}
