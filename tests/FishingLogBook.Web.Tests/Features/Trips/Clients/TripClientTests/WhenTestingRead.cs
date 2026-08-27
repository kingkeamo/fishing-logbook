using System.Net;
using AwesomeAssertions;

namespace FishingLogBook.Web.Tests.Features.Trips.Clients.TripClientTests;

public class WhenTestingRead : BaseTripClientTest
{
    [Fact]
    public async Task ItShouldThrowWhenTheTripListCannotBeRead()
    {
        // Arrange
        var handler = new RecordingHandler(HttpStatusCode.ServiceUnavailable, """{"errorMessage":"bad"}""");
        var client = CreateClient(handler);

        // Act
        var act = () => client.GetMyAsync(CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.AbsoluteUri.Should().Be("https://api.test/api/trips");
    }

    [Fact]
    public async Task ItShouldThrowWhenTheTripListPayloadIsMissing()
    {
        // Arrange
        var handler = new RecordingHandler(HttpStatusCode.OK, "null");
        var client = CreateClient(handler);

        // Act
        var act = () => client.GetMyAsync(CancellationToken.None);

        // Assert
        var exception = await act.Should().ThrowAsync<HttpRequestException>();
        exception.Which.Message.Should().Be("Trips were missing.");
    }

    [Fact]
    public async Task ItShouldReadTheTripSummariesWithTheirCounts()
    {
        // Arrange
        var body = $$"""
            [
              {
                "id": "{{TripId}}",
                "status": "Completed",
                "startedOn": "2026-08-17T09:00:00+00:00",
                "endedOn": "2026-08-17T14:00:00+00:00",
                "title": "Evening session",
                "placeName": "Lough Corrib",
                "catchCount": 3,
                "photographCount": 2,
                "noteCount": 1
              }
            ]
            """;
        var handler = new RecordingHandler(HttpStatusCode.OK, body);
        var client = CreateClient(handler);

        // Act
        var trips = await client.GetMyAsync(CancellationToken.None);

        // Assert
        trips.Should().ContainSingle();
        trips[0].Id.Should().Be(TripId);
        trips[0].PlaceName.Should().Be("Lough Corrib");
        trips[0].CatchCount.Should().Be(3);
        trips[0].PhotographCount.Should().Be(2);
        trips[0].NoteCount.Should().Be(1);
        handler.LastRequest!.RequestUri!.AbsoluteUri.Should().Be("https://api.test/api/trips");
    }

    [Fact]
    public async Task ItShouldReturnNothingWhenTheTripIsNotFound()
    {
        // Arrange
        var handler = new RecordingHandler(HttpStatusCode.NotFound, string.Empty);
        var client = CreateClient(handler);

        // Act
        var detail = await client.GetDetailAsync(TripId, CancellationToken.None);

        // Assert
        detail.Should().BeNull();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.AbsoluteUri.Should()
            .Be($"https://api.test/api/trips/{TripId:D}");
    }

    [Fact]
    public async Task ItShouldThrowWhenTheTripDetailReadFails()
    {
        // Arrange
        var handler = new RecordingHandler(HttpStatusCode.ServiceUnavailable, string.Empty);
        var client = CreateClient(handler);

        // Act
        var act = () => client.GetDetailAsync(TripId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task ItShouldReadTheTripDetailWithItsNotesPhotographsAndCatches()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        var body = $$"""
            {
              "trip": {
                "id": "{{TripId}}",
                "ownerUserId": "11111111-1111-1111-1111-111111111111",
                "status": "Completed",
                "startedOn": "2026-08-17T09:00:00+00:00",
                "endedOn": "2026-08-17T14:00:00+00:00",
                "placeName": "Lough Corrib"
              },
              "notes": [
                {
                  "id": "22222222-2222-2222-2222-222222222222",
                  "tripId": "{{TripId}}",
                  "text": "The wind dropped.",
                  "recordedOn": "2026-08-17T09:30:00+00:00",
                  "createdByUserId": "11111111-1111-1111-1111-111111111111"
                }
              ],
              "photographs": [
                {
                  "id": "33333333-3333-3333-3333-333333333333",
                  "contentType": "image/jpeg",
                  "addedOn": "2026-08-17T10:00:00+00:00",
                  "url": "https://storage.test/one.jpg?signed=1"
                }
              ],
              "catches": [
                {
                  "id": "{{catchId}}",
                  "caughtOn": "2026-08-17T11:00:00+00:00",
                  "speciesName": "Pike"
                }
              ]
            }
            """;
        var handler = new RecordingHandler(HttpStatusCode.OK, body);
        var client = CreateClient(handler);

        // Act
        var detail = await client.GetDetailAsync(TripId, CancellationToken.None);

        // Assert
        detail!.Trip.PlaceName.Should().Be("Lough Corrib");
        detail.Notes.Single().Text.Should().Be("The wind dropped.");
        detail.Photographs.Single().Url.Should().Be("https://storage.test/one.jpg?signed=1");
        detail.Catches.Single().Id.Should().Be(catchId);
        detail.Catches.Single().SpeciesName.Should().Be("Pike");
        handler.LastRequest!.RequestUri!.AbsoluteUri.Should()
            .Be($"https://api.test/api/trips/{TripId:D}");
    }
}
