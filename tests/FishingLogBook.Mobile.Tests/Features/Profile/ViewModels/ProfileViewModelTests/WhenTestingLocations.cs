using AwesomeAssertions;
using FishingLogBook.Mobile.Tests.Support;
using FishingLogBook.Shared.Constants;

namespace FishingLogBook.Mobile.Tests.Features.Profile.ViewModels.ProfileViewModelTests;

public sealed class WhenTestingLocations : BaseProfileViewModelTest
{
    [Fact]
    public async Task ItShouldRejectAnEmptyOrDuplicateLocation()
    {
        // Arrange
        await LoadAsync();

        // Act
        var empty = Sut.AddLocation("  ");
        var duplicate = Sut.AddLocation(" lough corrib ");

        // Assert
        empty.Should().BeFalse();
        duplicate.Should().BeFalse();
        Sut.Locations.Should().HaveCount(2);
        Sut.Message.Should().Be(Text["ProfileLocationDuplicate"]);
    }

    [Fact]
    public async Task ItShouldAddRenameRemoveAndChangeTheDefaultLocation()
    {
        // Arrange
        await LoadAsync();
        var moy = Sut.Locations.Single(location => location.Id == TestAnglerPreferences.MoyId);

        // Act
        var added = Sut.AddLocation("  Lough Mask  ");
        var renamed = Sut.RenameLocation(moy, "River Moy Upper");
        Sut.SetDefaultLocation(moy);
        Sut.RemoveLocation(Sut.Locations.Single(location => location.Name == "Lough Mask"));

        // Assert
        added.Should().BeTrue();
        renamed.Should().BeTrue();
        Sut.Locations.Should().HaveCount(2);
        Sut.Locations.Should().ContainSingle(location => location.IsDefault);
        moy.IsDefault.Should().BeTrue();
        moy.Name.Should().Be("River Moy Upper");
    }

    [Fact]
    public async Task ItShouldAcceptTheMaximumLocationLengthAndRejectOneCharacterMore()
    {
        // Arrange
        await LoadAsync();
        var maximum = new string('a', FishingLocationConstants.MaxNameLength);
        var tooLong = new string('b', FishingLocationConstants.MaxNameLength + 1);

        // Act
        var maximumResult = Sut.AddLocation(maximum);
        var tooLongResult = Sut.AddLocation(tooLong);

        // Assert
        maximumResult.Should().BeTrue();
        tooLongResult.Should().BeFalse();
        Sut.Locations.Should().ContainSingle(location => location.Name == maximum);
        Sut.Message.Should().Be(string.Format(
            Text.Culture,
            Text["ProfileLocationTooLong"],
            FishingLocationConstants.MaxNameLength));
    }
}
