using System.Net;
using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Features.Trips.Clients;
using FishingLogBook.Mobile.Tests.Api.Clients.AuthorizedApiClientTests;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Tests.Features.Trips.Clients.TripClientTests;

public sealed class WhenTestingRequests : BaseAuthorizedApiClientTest
{
    private static readonly Guid TripId = Guid.Parse("a1111111-1111-1111-1111-111111111111");
    private static readonly Guid ItemId = Guid.Parse("c3333333-3333-3333-3333-333333333333");

    [Fact]
    public async Task ItShouldDistinguishAMissingTripFromAnUnavailableServer()
    {
        // Arrange
        var responses = new Queue<HttpResponseMessage>([new HttpResponseMessage(HttpStatusCode.NotFound), new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)]);
        var (api, _) = CreateSut(_ => responses.Dequeue());
        var sut = new TripClient(api);
        // Act
        var missing = await sut.GetAsync(TripId, CancellationToken.None);
        var unavailable = await sut.GetAsync(TripId, CancellationToken.None);
        // Assert
        missing.Outcome.Should().Be(ApiOutcomeEnum.NotFound);
        unavailable.Outcome.Should().Be(ApiOutcomeEnum.ServerError);
        unavailable.Value.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldListTheTripsOfTheCurrentUser()
    {
        // Arrange
        var json = $$"""[{"id":"{{TripId}}","status":"Active","startedOn":"2026-10-31T09:00:00Z","title":"Corrib","role":"Owner","catchCount":2,"photographCount":1,"noteCount":3}]""";
        var (api, server) = CreateSut(_ => Json(json));
        var sut = new TripClient(api);
        // Act
        var result = await sut.ListAsync(CancellationToken.None);
        // Assert
        var request = server.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Get);
        request.Uri!.AbsolutePath.Should().Be("/api/trips");
        result.Value!.Single().CatchCount.Should().Be(2);
        result.Value!.Single().Role.Should().Be("Owner");
    }

    [Fact]
    public async Task ItShouldReadTheTripDetailWithItsTimelineContent()
    {
        // Arrange
        var json = $$"""{"trip":{"id":"{{TripId}}","ownerUserId":"{{UserId}}","status":"Active","startedOn":"2026-10-31T09:00:00Z"},"role":"Owner","notes":[{"id":"{{ItemId}}","tripId":"{{TripId}}","text":"Rising fish","recordedOn":"2026-10-31T10:00:00Z","createdByUserId":"{{UserId}}"}],"photographs":[],"catches":[],"contributors":[{"userId":"{{UserId}}","displayName":"Me","isOwner":true}]}""";
        var (api, server) = CreateSut(_ => Json(json));
        var sut = new TripClient(api);
        // Act
        var result = await sut.GetAsync(TripId, CancellationToken.None);
        // Assert
        server.Requests.Single().Uri!.AbsolutePath.Should().Be($"/api/trips/{TripId:D}");
        result.Value!.Notes.Single().Text.Should().Be("Rising fish");
        result.Value.Contributors.Single().IsOwner.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldPostTheTripWithItsClientGeneratedIdForAnIdempotentUpsert()
    {
        // Arrange
        string? body = null;
        var (api, server) = CreateSut(request =>
        {
            body = request.Content!.ReadAsStringAsync().Result;
            return Json($$"""{"id":"{{TripId}}","status":"Active","startedOn":"2026-10-31T09:00:00Z","ownerUserId":"{{UserId}}"}""");
        });
        var sut = new TripClient(api);
        // Act
        var result = await sut.UpsertAsync(new TripDto(TripId, "Active", DateTimeOffset.Parse("2026-10-31T09:00:00Z")) { OwnerUserId = UserId, Title = "Corrib" }, CancellationToken.None);
        // Assert
        var request = server.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Post);
        request.Uri!.AbsolutePath.Should().Be("/api/trips");
        body.Should().Contain(TripId.ToString("D")).And.Contain("Corrib");
        result.Value!.OwnerUserId.Should().Be(UserId);
    }

    [Fact]
    public async Task ItShouldPostANoteWithItsClientGeneratedIdAndDeleteItByIdAndTrip()
    {
        // Arrange
        string? body = null;
        var (api, server) = CreateSut(request =>
        {
            if (request.Method == HttpMethod.Post)
            {
                body = request.Content!.ReadAsStringAsync().Result;
                return Json($$"""{"id":"{{ItemId}}","tripId":"{{TripId}}","text":"Hello","recordedOn":"2026-10-31T10:00:00Z","createdByUserId":"{{UserId}}"}""");
            }

            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        var sut = new TripClient(api);
        // Act
        var recorded = await sut.RecordNoteAsync(TripId, new RecordTripNoteDto(ItemId, "Hello", DateTimeOffset.Parse("2026-10-31T10:00:00Z")), CancellationToken.None);
        var deleted = await sut.DeleteNoteAsync(TripId, ItemId, CancellationToken.None);
        // Assert
        server.Requests.Select(request => (request.Method.Method, request.Uri!.AbsolutePath)).Should().Equal(
            ("POST", $"/api/trips/{TripId:D}/notes"),
            ("DELETE", $"/api/trips/{TripId:D}/notes/{ItemId:D}"));
        body.Should().Contain(ItemId.ToString("D")).And.Contain("Hello");
        recorded.Value!.Id.Should().Be(ItemId);
        deleted.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldRequestAnUploadUrlThenRegisterAndDeleteATripPhotograph()
    {
        // Arrange
        var bodies = new List<string>();
        var (api, server) = CreateSut(request =>
        {
            if (request.Content is not null)
            {
                bodies.Add(request.Content.ReadAsStringAsync().Result);
            }

            return request.Method == HttpMethod.Delete ? new HttpResponseMessage(HttpStatusCode.NoContent)
                : request.RequestUri!.AbsolutePath.EndsWith("upload-url", StringComparison.Ordinal)
                    ? Json($$"""{"objectKey":"trips/key","uploadUrl":"https://upload.example/put"}""")
                    : Json($$"""{"id":"{{ItemId}}","tripId":"{{TripId}}","contentType":"image/jpeg","addedOn":"2026-10-31T10:00:00Z","contributedByUserId":"{{UserId}}"}""");
        });
        var sut = new TripClient(api);
        // Act
        var upload = await sut.CreatePhotographUploadAsync(TripId, new PhotographUploadRequestDto(ItemId, "image/jpeg"), CancellationToken.None);
        var recorded = await sut.RecordPhotographAsync(TripId, new RecordTripPhotographDto(ItemId, "trips/key", "image/jpeg", DateTimeOffset.Parse("2026-10-31T10:00:00Z"), null), CancellationToken.None);
        var deleted = await sut.DeletePhotographAsync(TripId, ItemId, CancellationToken.None);
        // Assert
        server.Requests.Select(request => (request.Method.Method, request.Uri!.AbsolutePath)).Should().Equal(
            ("POST", $"/api/trips/{TripId:D}/photographs/upload-url"),
            ("POST", $"/api/trips/{TripId:D}/photographs"),
            ("DELETE", $"/api/trips/{TripId:D}/photographs/{ItemId:D}"));
        bodies[1].Should().Contain("trips/key");
        upload.Value!.UploadUrl.Should().Be("https://upload.example/put");
        upload.Value.ObjectKey.Should().Be("trips/key");
        recorded.Value!.ContributedByUserId.Should().Be(UserId);
        deleted.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldPostTheCatchIdsToAssociateWithATrip()
    {
        // Arrange
        string? body = null;
        var (api, server) = CreateSut(request =>
        {
            body = request.Content!.ReadAsStringAsync().Result;
            return Json($$"""{"associatedCatchIds":["{{ItemId}}"],"rejectedCatchIds":[]}""");
        });
        var sut = new TripClient(api);
        // Act
        var result = await sut.AssociateCatchesAsync(TripId, new AssociateTripCatchesDto([ItemId]), CancellationToken.None);
        // Assert
        var request = server.Requests.Should().ContainSingle().Subject;
        request.Uri!.AbsolutePath.Should().Be($"/api/trips/{TripId:D}/catches");
        body.Should().Contain(ItemId.ToString("D"));
        result.IsSuccess.Should().BeTrue();
    }
}
