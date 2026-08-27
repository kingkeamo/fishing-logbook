using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;

namespace FishingLogBook.Web.Tests.Features.Catch.Offline.CatchJsonTests;

public class WhenTestingTripAssociation : BaseCatchJsonTest
{
    private static readonly Guid CatchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TripId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public void ItShouldReadAPreTripCatchWithNoTrip()
    {
        // Arrange
        const string json = """
            {"id":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","caughtOn":"2026-08-17T08:00:00Z",
             "speciesName":"Pike","photographs":[],"userId":"11111111-1111-1111-1111-111111111111",
             "syncStatus":"synchronised","metadataSyncStatus":"synchronised"}
            """;

        // Act
        var restored = CatchJson.DeserializeMetadata(json);

        // Assert
        restored.TripId.Should().BeNull();
        restored.SpeciesName.Should().Be("Pike");
        restored.SyncStatus.Should().Be(Web.Common.SyncStatus.Synchronised);
    }

    [Fact]
    public void ItShouldReadAStoredNullTripAsNoTrip()
    {
        // Arrange
        const string json = """
            {"id":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","caughtOn":"2026-08-17T08:00:00Z",
             "photographs":[],"tripId":null}
            """;

        // Act
        var restored = CatchJson.DeserializeMetadata(json);

        // Assert
        restored.TripId.Should().BeNull();
    }

    [Fact]
    public void ItShouldNotWriteATripWhenTheCatchHasNone()
    {
        // Arrange
        var model = Catch(tripId: null);

        // Act
        var json = CatchJson.SerializeMetadata(model);

        // Assert
        json.Should().Contain("\"tripId\":null");
        CatchJson.DeserializeMetadata(json).TripId.Should().BeNull();
    }

    [Fact]
    public void ItShouldRoundTripTheAssociatedTripThroughMetadata()
    {
        // Arrange
        var model = Catch(TripId);

        // Act
        var json = CatchJson.SerializeMetadata(model);
        var restored = CatchJson.DeserializeMetadata(json);

        // Assert
        json.Should().Contain($"\"tripId\":\"{TripId:D}\"");
        restored.TripId.Should().Be(TripId);
        restored.Photographs.Should().AllSatisfy(photograph => photograph.Bytes.Should().BeNull());
    }

    [Fact]
    public void ItShouldRoundTripTheAssociatedTripAlongsidePhotographBytes()
    {
        // Arrange
        var model = Catch(TripId);

        // Act
        var json = CatchJson.SerializeMetadata(model);
        var restored = CatchJson.Deserialize(json, model.Photographs);

        // Assert
        restored.TripId.Should().Be(TripId);
        restored.Photographs.Should().ContainSingle();
        restored.Photographs[0].Bytes.Should().Equal(1);
    }

    private static CatchModel Catch(Guid? tripId)
    {
        return new CatchModel(
            CatchId,
            DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            [new CatchPhotographModel(CatchId, CatchId, PhotographContentTypeConstants.Jpeg, [1])],
            TripId: tripId);
    }
}
