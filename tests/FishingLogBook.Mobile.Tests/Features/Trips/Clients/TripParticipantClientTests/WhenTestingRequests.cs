using System.Net;
using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Features.Trips.Clients;
using FishingLogBook.Mobile.Tests.Api.Clients.AuthorizedApiClientTests;

namespace FishingLogBook.Mobile.Tests.Features.Trips.Clients.TripParticipantClientTests;

public sealed class WhenTestingRequests : BaseAuthorizedApiClientTest
{
    private static readonly Guid TripId = Guid.Parse("a1111111-1111-1111-1111-111111111111");
    private static readonly Guid FriendId = Guid.Parse("b2222222-2222-2222-2222-222222222222");

    private static string ParticipantsJson => $$"""{"tripId":"{{TripId}}","role":"Owner","participants":[{"userId":"{{FriendId}}","status":"Pending","displayName":"Sam","invitedOn":"2026-10-31T12:00:00Z","isOwner":false}]}""";

    [Fact]
    public async Task ItShouldRejectInvitationRepliesThatAreNotNoContentFailures()
    {
        // Arrange
        var (api, _) = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));
        var sut = new TripParticipantClient(api);
        // Act
        var result = await sut.RespondAsync(TripId, accept: true, CancellationToken.None);
        // Assert
        result.Outcome.Should().Be(ApiOutcomeEnum.Forbidden);
    }

    [Fact]
    public async Task ItShouldReturnTheParticipantsWhenRemovingAnAngler()
    {
        // Arrange
        string? body = null;
        var (api, server) = CreateSut(request =>
        {
            body = request.Content?.ReadAsStringAsync().Result;
            return Json(ParticipantsJson);
        });
        var sut = new TripParticipantClient(api);
        // Act
        var result = await sut.RemoveAsync(TripId, FriendId, CancellationToken.None);
        // Assert
        var request = server.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Delete);
        request.Uri!.AbsolutePath.Should().Be($"/api/trips/{TripId:D}/participants/{FriendId:D}");
        body.Should().BeNullOrEmpty();
        result.Value!.Participants.Single().UserId.Should().Be(FriendId);
    }

    [Fact]
    public async Task ItShouldPostTheInvitedUserIdAndReturnTheUpdatedParticipants()
    {
        // Arrange
        string? body = null;
        var (api, server) = CreateSut(request =>
        {
            body = request.Content!.ReadAsStringAsync().Result;
            return Json(ParticipantsJson);
        });
        var sut = new TripParticipantClient(api);
        // Act
        var result = await sut.InviteAsync(TripId, FriendId, CancellationToken.None);
        // Assert
        var request = server.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Post);
        request.Uri!.AbsolutePath.Should().Be($"/api/trips/{TripId:D}/participants");
        body.Should().Contain(FriendId.ToString("D"));
        result.Value!.Participants.Should().ContainSingle(participant => participant.Status == "Pending");
        request.Authorization.Should().Be("Bearer " + AccessToken);
    }

    [Fact]
    public async Task ItShouldReadTheParticipantsAndDistinguishAMissingTrip()
    {
        // Arrange
        var responses = new Queue<HttpResponseMessage>([Json(ParticipantsJson), new HttpResponseMessage(HttpStatusCode.NotFound)]);
        var (api, server) = CreateSut(_ => responses.Dequeue());
        var sut = new TripParticipantClient(api);
        // Act
        var found = await sut.GetAsync(TripId, CancellationToken.None);
        var missing = await sut.GetAsync(TripId, CancellationToken.None);
        // Assert
        server.Requests.Select(request => (request.Method, request.Uri!.AbsolutePath)).Should()
            .AllBeEquivalentTo((HttpMethod.Get, $"/api/trips/{TripId:D}/participants"));
        found.Value!.Role.Should().Be("Owner");
        missing.Outcome.Should().Be(ApiOutcomeEnum.NotFound);
    }

    [Fact]
    public async Task ItShouldListTheCurrentUsersInvitations()
    {
        // Arrange
        var json = $$"""[{"tripId":"{{TripId}}","ownerUserId":"{{FriendId}}","ownerDisplayName":"Sam","title":"Corrib","placeName":null,"startedOn":"2026-10-31T09:00:00Z","invitedOn":"2026-10-31T10:00:00Z"}]""";
        var (api, server) = CreateSut(_ => Json(json));
        var sut = new TripParticipantClient(api);
        // Act
        var result = await sut.ListInvitationsAsync(CancellationToken.None);
        // Assert
        server.Requests.Single().Uri!.AbsolutePath.Should().Be("/api/trips/invitations");
        result.Value.Should().ContainSingle(invitation => invitation.TripId == TripId && invitation.OwnerDisplayName == "Sam");
    }

    [Theory]
    [InlineData(true, "accept")]
    [InlineData(false, "decline")]
    public async Task ItShouldPostTheInvitationResponseToTheMatchingRoute(bool accept, string action)
    {
        // Arrange
        var (api, server) = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var sut = new TripParticipantClient(api);
        // Act
        var result = await sut.RespondAsync(TripId, accept, CancellationToken.None);
        // Assert
        var request = server.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Post);
        request.Uri!.AbsolutePath.Should().Be($"/api/trips/{TripId:D}/invitation/{action}");
        result.IsSuccess.Should().BeTrue();
    }
}
