using System.Net;
using AwesomeAssertions;

namespace FishingLogBook.Web.Tests.Features.Trips.Clients.TripParticipantClientTests;

public class WhenTestingRemove : BaseTripParticipantClientTest
{
    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task ItShouldReturnNothingWhenTheApiRefusesTheRemoval(HttpStatusCode status)
    {
        // Arrange
        var handler = new RecordingHandler(status);
        var client = CreateClient(handler);

        // Act
        var result = await client.RemoveAsync(TripId, UserId, CancellationToken.None);

        // Assert
        result.Should().BeNull();
        handler.Calls.Should().Be(1);
    }

    [Fact]
    public async Task ItShouldDeleteTheParticipantAndReadTheRemainingParticipants()
    {
        // Arrange
        var handler = new RecordingHandler(HttpStatusCode.OK, $$"""{"tripId":"{{TripId}}","role":"Owner"}""");
        var client = CreateClient(handler);

        // Act
        var result = await client.RemoveAsync(TripId, UserId, CancellationToken.None);

        // Assert
        result!.TripId.Should().Be(TripId);
        handler.LastRequest!.Method.Should().Be(HttpMethod.Delete);
        handler.LastRequest.RequestUri!.AbsoluteUri.Should()
            .Be($"https://api.test/api/trips/{TripId:D}/participants/{UserId:D}");
    }
}
