using System.Net;
using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Features.Trips.Clients;
using FishingLogBook.Mobile.Tests.Api.Clients.AuthorizedApiClientTests;

namespace FishingLogBook.Mobile.Tests.Features.Trips.Clients.TripClientTests;

public sealed class WhenTestingPlannedRequests : BaseAuthorizedApiClientTest
{
    private static readonly Guid TripId = Guid.Parse("a1111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task ItShouldReportAFailedPlannedRead()
    {
        // Arrange
        var (api, _) = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var sut = new TripClient(api);

        // Act
        var result = await sut.ListPlannedAsync(null, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldReadThePlannedTripsWithoutAWindow()
    {
        // Arrange
        var json = $$"""[{"tripId":"{{TripId}}","plannedStartOn":"2026-11-02T09:00:00Z","organiserUserId":"{{UserId}}","organiserName":"Sam","title":"Corrib","role":"Participant","participants":[{"userId":"{{UserId}}","displayName":"Pat","status":"Pending"}]}]""";
        var (api, server) = CreateSut(_ => Json(json));
        var sut = new TripClient(api);

        // Act
        var result = await sut.ListPlannedAsync(null, CancellationToken.None);

        // Assert
        var request = server.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Get);
        request.Uri!.PathAndQuery.Should().Be("/api/trips/planned");
        result.Value!.Single().OrganiserName.Should().Be("Sam");
        result.Value!.Single().Participants.Single().Status.Should().Be("Pending");
    }

    [Fact]
    public async Task ItShouldPassTheWindowWhenReadingPlannedTrips()
    {
        // Arrange
        var (api, server) = CreateSut(_ => Json("[]"));
        var sut = new TripClient(api);

        // Act
        await sut.ListPlannedAsync(5, CancellationToken.None);

        // Assert
        server.Requests.Single().Uri!.PathAndQuery.Should().Be("/api/trips/planned?windowDays=5");
    }

    [Fact]
    public async Task ItShouldDistinguishAMissingTripWhenActivating()
    {
        // Arrange
        var (api, _) = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var sut = new TripClient(api);

        // Act
        var result = await sut.ActivateAsync(TripId, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(ApiOutcomeEnum.NotFound);
    }

    [Fact]
    public async Task ItShouldPostTheActivationAndReadTheActivatedTrip()
    {
        // Arrange
        var json = $$"""{"id":"{{TripId}}","ownerUserId":"{{UserId}}","status":"Active","startedOn":"2026-11-02T09:05:00Z","plannedStartOn":"2026-11-02T09:00:00Z"}""";
        var (api, server) = CreateSut(_ => Json(json));
        var sut = new TripClient(api);

        // Act
        var result = await sut.ActivateAsync(TripId, CancellationToken.None);

        // Assert
        var request = server.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Post);
        request.Uri!.AbsolutePath.Should().Be($"/api/trips/{TripId:D}/activate");
        result.Value!.Status.Should().Be("Active");
        result.Value.PlannedStartOn.Should().Be(DateTimeOffset.Parse("2026-11-02T09:00:00Z"));
    }
}
