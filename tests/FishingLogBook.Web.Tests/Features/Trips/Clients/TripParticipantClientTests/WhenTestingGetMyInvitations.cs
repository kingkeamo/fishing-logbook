using System.Net;
using AwesomeAssertions;

namespace FishingLogBook.Web.Tests.Features.Trips.Clients.TripParticipantClientTests;

public class WhenTestingGetMyInvitations : BaseTripParticipantClientTest
{
    [Fact]
    public async Task ItShouldThrowWhenTheApiFails()
    {
        // Arrange
        var handler = new RecordingHandler(HttpStatusCode.ServiceUnavailable);
        var client = CreateClient(handler);

        // Act
        var act = () => client.GetMyInvitationsAsync(CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task ItShouldReturnNoInvitationsWhenThePayloadIsNull()
    {
        // Arrange
        var handler = new RecordingHandler(HttpStatusCode.OK, "null");
        var client = CreateClient(handler);

        // Act
        var result = await client.GetMyInvitationsAsync(CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
        handler.LastRequest!.RequestUri!.AbsoluteUri.Should().Be("https://api.test/api/trips/invitations");
    }

    [Fact]
    public async Task ItShouldReadTheInvitations()
    {
        // Arrange
        var body = $$"""
            [{
              "tripId": "{{TripId}}",
              "ownerUserId": "{{UserId}}",
              "ownerDisplayName": "Sam",
              "title": "Lough trip",
              "placeName": "Lough Corrib",
              "startedOn": "2026-09-12T06:30:00+00:00",
              "invitedOn": "2026-09-10T08:00:00+00:00"
            }]
            """;
        var handler = new RecordingHandler(HttpStatusCode.OK, body);
        var client = CreateClient(handler);

        // Act
        var result = await client.GetMyInvitationsAsync(CancellationToken.None);

        // Assert
        var invitation = result.Should().ContainSingle().Subject;
        invitation.TripId.Should().Be(TripId);
        invitation.OwnerDisplayName.Should().Be("Sam");
        invitation.Title.Should().Be("Lough trip");
        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
    }
}
