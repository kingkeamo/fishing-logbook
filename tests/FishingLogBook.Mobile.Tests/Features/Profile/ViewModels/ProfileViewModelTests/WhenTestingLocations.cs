using AwesomeAssertions;
using FishingLogBook.Mobile.Interactions;
using FishingLogBook.Mobile.Tests.Support;
using FishingLogBook.Shared.Constants;
using NSubstitute;

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
    public async Task ItShouldIgnoreADefaultRequestForALocationThatWasRemoved()
    {
        // Arrange
        await LoadAsync();
        var moy = Sut.Locations.Single(location => location.Id == TestAnglerPreferences.MoyId);
        await Sut.RemoveLocationAsync(moy);

        // Act
        Sut.SetDefaultLocation(moy);

        // Assert
        Sut.Locations.Should().ContainSingle(location => location.IsDefault)
            .Which.Id.Should().Be(TestAnglerPreferences.CorribId);
    }

    [Fact]
    public async Task ItShouldPromoteTheFirstRemainingLocationWhenTheDefaultIsRemoved()
    {
        // Arrange
        await LoadAsync();
        Sut.AddLocation("Lough Mask");
        var corrib = Sut.Locations.Single(location => location.Id == TestAnglerPreferences.CorribId);

        // Act
        await Sut.RemoveLocationAsync(corrib);

        // Assert
        Sut.Locations.Should().HaveCount(2);
        Sut.Locations.Should().ContainSingle(location => location.IsDefault)
            .Which.Id.Should().Be(TestAnglerPreferences.MoyId);
    }

    [Fact]
    public async Task ItShouldLeaveNoDefaultWhenTheLastLocationIsRemovedAndDefaultTheNextOneAdded()
    {
        // Arrange
        await LoadAsync();
        await Sut.RemoveLocationAsync(Sut.Locations[0]);
        await Sut.RemoveLocationAsync(Sut.Locations[0]);
        var emptied = Sut.Locations.ToArray();

        // Act
        Sut.AddLocation("Lough Mask");

        // Assert
        emptied.Should().BeEmpty();
        Sut.Locations.Should().ContainSingle().Which.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldKeepTheExistingDefaultWhenALocationIsAdded()
    {
        // Arrange
        await LoadAsync();

        // Act
        Sut.AddLocation("Lough Mask");

        // Assert
        Sut.Locations.Should().ContainSingle(location => location.IsDefault)
            .Which.Id.Should().Be(TestAnglerPreferences.CorribId);
    }

    [Fact]
    public async Task ItShouldMoveTheDefaultToANewlyAddedLocationWhenChosen()
    {
        // Arrange
        await LoadAsync();
        Sut.AddLocation("Lough Mask");
        var mask = Sut.Locations.Single(location => location.Name == "Lough Mask");

        // Act
        Sut.SetDefaultLocation(mask);

        // Assert
        Sut.Locations.Should().ContainSingle(location => location.IsDefault).Which.Should().BeSameAs(mask);
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
        await Sut.RemoveLocationAsync(Sut.Locations.Single(location => location.Name == "Lough Mask"));

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

    [Fact]
    public async Task ItShouldRemoveANonDefaultLocationWithoutAsking()
    {
        // Arrange
        await LoadAsync();
        var moy = Sut.Locations.Single(location => location.Id == TestAnglerPreferences.MoyId);

        // Act
        await Sut.RemoveLocationAsync(moy);

        // Assert
        Sut.Locations.Should().ContainSingle(location => location.Id == TestAnglerPreferences.CorribId);
        await MockPresenter.DidNotReceive().ShowDialogAsync(Arg.Any<DialogRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAskBeforeRemovingTheDefaultLocation()
    {
        // Arrange
        await LoadAsync();
        var corrib = Sut.Locations.Single(location => location.Id == TestAnglerPreferences.CorribId);

        // Act
        await Sut.RemoveLocationAsync(corrib);

        // Assert
        Sut.Locations.Should().ContainSingle(location => location.Id == TestAnglerPreferences.MoyId);
        await MockPresenter.Received(1).ShowDialogAsync(
            Arg.Is<DialogRequest>(request =>
                request.IsDestructive &&
                request.Title == Text["ProfileRemoveDefaultLocationTitle"] &&
                request.Message == Text["ProfileRemoveDefaultLocationBody"] &&
                request.Accept == Text["Remove"] &&
                request.Cancel == Text["Cancel"]),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepTheDefaultLocationWhenRemovalIsCancelled()
    {
        // Arrange
        await LoadAsync();
        MockPresenter.ShowDialogAsync(Arg.Any<DialogRequest>(), Arg.Any<CancellationToken>()).Returns(false);
        var corrib = Sut.Locations.Single(location => location.Id == TestAnglerPreferences.CorribId);

        // Act
        await Sut.RemoveLocationAsync(corrib);

        // Assert
        Sut.Locations.Should().HaveCount(2);
        corrib.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldAskBeforeRemovingTheLastLocation()
    {
        // Arrange
        await LoadAsync();
        await Sut.RemoveLocationAsync(Sut.Locations.Single(location => location.Id == TestAnglerPreferences.MoyId));
        var last = Sut.Locations.Single();

        // Act
        await Sut.RemoveLocationAsync(last);

        // Assert
        Sut.Locations.Should().BeEmpty();
        await MockPresenter.Received(1).ShowDialogAsync(
            Arg.Is<DialogRequest>(request =>
                request.IsDestructive &&
                request.Title == Text["ProfileRemoveDefaultLocationTitle"] &&
                request.Message == Text["ProfileRemoveLastLocationBody"] &&
                request.Accept == Text["Remove"] &&
                request.Cancel == Text["Cancel"]),
            Arg.Any<CancellationToken>());
    }
}
