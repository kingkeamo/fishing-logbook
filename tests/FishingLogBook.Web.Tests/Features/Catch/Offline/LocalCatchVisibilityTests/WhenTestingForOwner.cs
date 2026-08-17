using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Catch.Offline;

namespace FishingLogBook.Web.Tests.Features.Catch.Offline.LocalCatchVisibilityTests;

public class WhenTestingForOwner : BaseLocalCatchVisibilityTest
{
    [Fact]
    public void ItShouldReturnNothingWhenTheOwnerIsEmpty()
    {
        // Arrange
        var records = new[]
        {
            CatchFor(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), OwnerUserId)
        };

        // Act
        var visible = LocalCatchVisibility.ForOwner(records, Guid.Empty);

        // Assert
        visible.Should().BeEmpty();
    }

    [Fact]
    public void ItShouldNotExposeALegacyUnownedCatchToTheFirstSignedInUser()
    {
        // Arrange
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var location = new CatchLocationModel(
            53.2707,
            -9.0568,
            12,
            DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            LocationDefaults.DeviceGps,
            LocationDefaults.Private,
            LocationDefaults.ConsentVersion);
        var records = new[]
        {
            CatchFor(catchId, Guid.Empty, location)
        };

        // Act
        var firstSignedIn = LocalCatchVisibility.ForOwner(records, OtherUserId);
        var originalOwner = LocalCatchVisibility.ForOwner(records, OwnerUserId);

        // Assert
        firstSignedIn.Should().BeEmpty();
        originalOwner.Should().BeEmpty();
        records[0].UserId.Should().Be(Guid.Empty);
        records[0].Location.Should().Be(location);
    }

    [Fact]
    public void ItShouldKeepCatchesAlreadyOwnedByTheSignedInUser()
    {
        // Arrange
        var ownedId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var otherId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var unscopedId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var records = new[]
        {
            CatchFor(ownedId, OwnerUserId),
            CatchFor(otherId, OtherUserId),
            CatchFor(unscopedId, Guid.Empty)
        };

        // Act
        var visible = LocalCatchVisibility.ForOwner(records, OwnerUserId);

        // Assert
        visible.Should().ContainSingle();
        visible[0].Id.Should().Be(ownedId);
        visible[0].UserId.Should().Be(OwnerUserId);
    }
}
