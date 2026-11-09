using System.Net;
using AwesomeAssertions;

namespace FishingLogBook.Web.Tests.Features.Trips.Clients.TripParticipantClientTests;

public class WhenTestingRespond : BaseTripParticipantClientTest
{
    [Fact]
    public async Task ItShouldReportFailureWhenAcceptingIsRefused()
    {
        // Arrange
        var handler = new RecordingHandler(HttpStatusCode.Conflict);
        var client = CreateClient(handler);

        // Act
        var result = await client.AcceptAsync(TripId, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
        handler.Calls.Should().Be(1);
    }

    [Fact]
    public async Task ItShouldReportFailureWhenDecliningIsRefused()
    {
        // Arrange
        var handler = new RecordingHandler(HttpStatusCode.NotFound);
        var client = CreateClient(handler);

        // Act
        var result = await client.DeclineAsync(TripId, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
        handler.Calls.Should().Be(1);
    }

    [Fact]
    public async Task ItShouldPostAnAcceptanceToTheInvitationEndpoint()
    {
        // Arrange
        var handler = new RecordingHandler(HttpStatusCode.NoContent);
        var client = CreateClient(handler);

        // Act
        var result = await client.AcceptAsync(TripId, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.AbsoluteUri.Should()
            .Be($"https://api.test/api/trips/{TripId:D}/invitation/accept");
    }

    [Fact]
    public async Task ItShouldPostADeclineToTheInvitationEndpoint()
    {
        // Arrange
        var handler = new RecordingHandler(HttpStatusCode.NoContent);
        var client = CreateClient(handler);

        // Act
        var result = await client.DeclineAsync(TripId, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.AbsoluteUri.Should()
            .Be($"https://api.test/api/trips/{TripId:D}/invitation/decline");
    }
}
