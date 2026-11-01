using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Web.Tests.Features.Trips.Clients.TripClientTests;

public class WhenTestingPlanned : BaseTripClientTest
{
    [Fact]
    public async Task ItShouldThrowWhenThePlannedTripsCannotBeRead()
    {
        // Arrange
        var handler = new RecordingHandler(HttpStatusCode.ServiceUnavailable, """{"errorMessage":"bad"}""");
        var client = CreateClient(handler);

        // Act
        var act = () => client.GetPlannedAsync(null, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.AbsoluteUri.Should().Be("https://api.test/api/trips/planned");
    }

    [Fact]
    public async Task ItShouldThrowWhenThePlannedTripsPayloadIsMissing()
    {
        // Arrange
        var handler = new RecordingHandler(HttpStatusCode.OK, "null");
        var client = CreateClient(handler);

        // Act
        var act = () => client.GetPlannedAsync(null, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
        handler.Calls.Should().Be(1);
    }

    [Fact]
    public async Task ItShouldPassTheWindowWhenReadingPlannedTrips()
    {
        // Arrange
        var handler = new RecordingHandler(HttpStatusCode.OK, "[]");
        var client = CreateClient(handler);

        // Act
        var result = await client.GetPlannedAsync(5, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
        handler.Calls.Should().Be(1);
        handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
        handler.LastRequest.RequestUri!.AbsoluteUri.Should().Be("https://api.test/api/trips/planned?windowDays=5");
    }

    [Fact]
    public async Task ItShouldReadThePlannedTripsWithOrganiserAndParticipants()
    {
        // Arrange
        var organiserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var guestId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var plannedStart = DateTimeOffset.Parse("2026-09-12T06:30:00Z");
        var option = new PlannedTripOptionDto(TripId, plannedStart, organiserId, "Alex")
        {
            Title = "Lough Corrib",
            Participants = [new PlannedTripParticipantDto(guestId, "Sam", "Accepted")]
        };
        var handler = new RecordingHandler(HttpStatusCode.OK, JsonSerializer.Serialize(new[] { option }));
        var client = CreateClient(handler);

        // Act
        var result = await client.GetPlannedAsync(null, CancellationToken.None);

        // Assert
        result.Should().ContainSingle();
        result[0].TripId.Should().Be(TripId);
        result[0].PlannedStartOn.Should().Be(plannedStart);
        result[0].OrganiserName.Should().Be("Alex");
        result[0].Participants.Should().ContainSingle(guest => guest.DisplayName == "Sam");
    }

    [Fact]
    public async Task ItShouldThrowWhenActivationIsRejected()
    {
        // Arrange
        var handler = new RecordingHandler(HttpStatusCode.Conflict, """{"errorMessage":"active"}""");
        var client = CreateClient(handler);

        // Act
        var act = () => client.ActivateAsync(TripId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.AbsoluteUri.Should()
            .Be($"https://api.test/api/trips/{TripId:D}/activate");
    }

    [Fact]
    public async Task ItShouldPostTheActivation()
    {
        // Arrange
        var handler = new RecordingHandler(HttpStatusCode.OK, "{}");
        var client = CreateClient(handler);

        // Act
        await client.ActivateAsync(TripId, CancellationToken.None);

        // Assert
        handler.Calls.Should().Be(1);
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.AbsoluteUri.Should()
            .Be($"https://api.test/api/trips/{TripId:D}/activate");
    }
}
