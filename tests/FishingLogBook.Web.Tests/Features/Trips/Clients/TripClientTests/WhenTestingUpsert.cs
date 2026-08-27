using System.Net;
using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Web.Tests.Features.Trips.Clients.TripClientTests;

public class WhenTestingUpsert : BaseTripClientTest
{
    [Fact]
    public async Task ItShouldThrowWhenTheServerRejectsTheTrip()
    {
        // Arrange
        var handler = new RecordingHandler(HttpStatusCode.BadRequest);
        var client = CreateClient(handler);

        // Act
        var act = async () => await client.UpsertAsync(CreateTrip(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
        handler.Calls.Should().Be(1);
    }

    [Fact]
    public async Task ItShouldThrowWhenTheServerIsUnavailable()
    {
        // Arrange
        var handler = new RecordingHandler(HttpStatusCode.ServiceUnavailable);
        var client = CreateClient(handler);

        // Act
        var act = async () => await client.UpsertAsync(CreateTrip(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task ItShouldPostTheTripToTheTripsRoute()
    {
        // Arrange
        var handler = new RecordingHandler(HttpStatusCode.OK, "null");
        var client = CreateClient(handler);
        var location = new TripLocationDto(
            53.2707,
            -9.0568,
            7,
            StartedOn.AddMinutes(-1),
            "DeviceGps",
            "Private",
            "1");

        // Act
        await client.UpsertAsync(
            CreateTrip(location: location),
            CancellationToken.None);

        // Assert
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.ToString().Should().Be("https://api.test/api/trips");
        handler.LastBody.Should().Contain($"\"{TripId:D}\"");
        handler.LastBody.Should().Contain("Corrib shoreline");
        handler.LastBody.Should().Contain("53.2707");
    }

    [Fact]
    public async Task ItShouldReturnTheServerLifecycleForTheTrip()
    {
        // Arrange
        var endedOn = StartedOn.AddHours(2);
        var body = $$"""
            {
                "id": "{{TripId:D}}",
                "status": "{{TripConstants.Completed}}",
                "startedOn": "2026-08-17T09:00:00+00:00",
                "endedOn": "{{endedOn:O}}",
                "location": null,
                "title": "Evening session",
                "placeName": "Corrib shoreline"
            }
            """;
        var client = CreateClient(new RecordingHandler(HttpStatusCode.OK, body));

        // Act
        var result = await client.UpsertAsync(CreateTrip(), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(TripId);
        result.Status.Should().Be(TripConstants.Completed);
        result.EndedOn.Should().Be(endedOn);
    }
}
