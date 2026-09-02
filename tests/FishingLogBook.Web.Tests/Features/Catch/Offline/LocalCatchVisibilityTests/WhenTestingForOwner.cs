using AwesomeAssertions;
using FishingLogBook.Web.Features.Catch.Offline;

namespace FishingLogBook.Web.Tests.Features.Catch.Offline.LocalCatchVisibilityTests;

public class WhenTestingForOwner : BaseLocalCatchVisibilityTest
{
    [Fact]
    public void ItShouldHideUnscopedCatchesFromTheFirstSignedInUser()
    {
        // Arrange
        var unscoped = CatchFor(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), Guid.Empty);

        // Act
        var visible = LocalCatchVisibility.ForOwner([unscoped], OtherUserId);

        // Assert
        visible.Should().BeEmpty();
    }

    [Fact]
    public void ItShouldHideAnotherUsersCatch()
    {
        // Arrange
        var ownedByOther = CatchFor(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), OtherUserId);

        // Act
        var visible = LocalCatchVisibility.ForOwner([ownedByOther], OwnerUserId);

        // Assert
        visible.Should().BeEmpty();
    }

    [Fact]
    public void ItShouldHideUnscopedCatchesEvenWhenTheCallerAlsoHasOwnedRecords()
    {
        // Arrange
        var unscoped = CatchFor(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), Guid.Empty);
        var owned = CatchFor(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), OwnerUserId);

        // Act
        var visible = LocalCatchVisibility.ForOwner([unscoped, owned], OwnerUserId);

        // Assert
        visible.Should().ContainSingle();
        visible[0].Id.Should().Be(owned.Id);
        visible[0].CaughtByUserId.Should().Be(OwnerUserId);
    }

    [Fact]
    public void ItShouldReturnNothingWhenTheOwnerIdIsEmpty()
    {
        // Arrange
        var owned = CatchFor(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), OwnerUserId);

        // Act
        var visible = LocalCatchVisibility.ForOwner([owned], Guid.Empty);

        // Assert
        visible.Should().BeEmpty();
    }

    [Fact]
    public void ItShouldReturnCatchesOwnedByTheCaller()
    {
        // Arrange
        var owned = CatchFor(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), OwnerUserId);

        // Act
        var visible = LocalCatchVisibility.ForOwner([owned], OwnerUserId);

        // Assert
        visible.Should().ContainSingle();
        visible[0].Should().Be(owned);
    }
}
