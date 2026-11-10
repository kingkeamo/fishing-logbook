using System.Net;
using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Web.Tests.Features.Trips.Clients.TripParticipantClientTests;

public class WhenTestingInvite : BaseTripParticipantClientTest
{
    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task ItShouldReturnNothingWhenTheApiRefusesTheInvitation(HttpStatusCode status)
    {
        // Arrange
        var handler = new RecordingHandler(status);
        var client = CreateClient(handler);

        // Act
        var result = await client.InviteAsync(TripId, new InviteTripParticipantDto(UserId), CancellationToken.None);

        // Assert
        result.Should().BeNull();
        handler.Calls.Should().Be(1);
    }

    [Fact]
    public async Task ItShouldPostTheInvitedUserAndReadTheParticipants()
    {
        // Arrange
        var handler = new RecordingHandler(HttpStatusCode.OK, $$"""{"tripId":"{{TripId}}","role":"Owner"}""");
        var client = CreateClient(handler);

        // Act
        var result = await client.InviteAsync(TripId, new InviteTripParticipantDto(UserId), CancellationToken.None);

        // Assert
        result!.TripId.Should().Be(TripId);
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.AbsoluteUri.Should().Be($"https://api.test/api/trips/{TripId:D}/participants");
        handler.LastBody.Should().Contain(UserId.ToString("D"));
    }
}
