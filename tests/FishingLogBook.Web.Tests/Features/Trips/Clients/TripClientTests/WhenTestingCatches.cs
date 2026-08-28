using System.Net;
using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Web.Tests.Features.Trips.Clients.TripClientTests;

public class WhenTestingCatches : BaseTripClientTest
{
    private static readonly Guid PikeCatchId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    private static readonly Guid TroutCatchId = Guid.Parse("cccccccc-0000-0000-0000-000000000002");

    [Fact]
    public async Task ItShouldThrowWhenTheServerRejectsTheAssociation()
    {
        // Arrange
        var handler = new RecordingHandler(HttpStatusCode.BadRequest);
        var client = CreateClient(handler);

        // Act
        var act = async () => await client.AssociateCatchesAsync(
            TripId,
            new AssociateTripCatchesDto([PikeCatchId]),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
        handler.LastRequest!.RequestUri!.ToString()
            .Should().Be($"https://api.test/api/trips/{TripId:D}/catches");
    }

    [Fact]
    public async Task ItShouldThrowWhenTheServerIsUnavailable()
    {
        // Arrange
        var handler = new RecordingHandler(HttpStatusCode.ServiceUnavailable);
        var client = CreateClient(handler);

        // Act
        var act = async () => await client.AssociateCatchesAsync(
            TripId,
            new AssociateTripCatchesDto([PikeCatchId]),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task ItShouldReturnNullWhenTheBodyIsMissing()
    {
        // Arrange
        var client = CreateClient(new RecordingHandler(HttpStatusCode.OK, "null"));

        // Act
        var association = await client.AssociateCatchesAsync(
            TripId,
            new AssociateTripCatchesDto([PikeCatchId]),
            CancellationToken.None);

        // Assert
        association.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldPostTheCatchIdsToTheTripCatchesRoute()
    {
        // Arrange
        var body = $$"""
            {
                "associatedCatchIds": ["{{PikeCatchId:D}}"],
                "rejectedCatchIds": ["{{TroutCatchId:D}}"]
            }
            """;
        var handler = new RecordingHandler(HttpStatusCode.OK, body);
        var client = CreateClient(handler);

        // Act
        var association = await client.AssociateCatchesAsync(
            TripId,
            new AssociateTripCatchesDto([PikeCatchId, TroutCatchId]),
            CancellationToken.None);

        // Assert
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.ToString()
            .Should().Be($"https://api.test/api/trips/{TripId:D}/catches");
        handler.LastBody.Should().Contain($"\"{PikeCatchId:D}\"");
        handler.LastBody.Should().Contain($"\"{TroutCatchId:D}\"");
        association!.AssociatedCatchIds.Should().Equal(PikeCatchId);
        association.RejectedCatchIds.Should().Equal(TroutCatchId);
    }
}
