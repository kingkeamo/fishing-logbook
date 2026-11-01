using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Trips.Models;
using NSubstitute;
using static FishingLogBook.Mobile.Tests.Features.Trips.TestSupport.TripTestData;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.TripAssociationViewModelTests;

public sealed class WhenTestingSelection : BaseTripAssociationViewModelTest
{
    private static readonly TripAnglerOptionModel Me = new(ViewerId, "Me", true);
    private static readonly TripAnglerOptionModel Sam = new(FriendId, "Sam", false);

    private async Task JoinAsync(params TripAnglerOptionModel[] anglers)
    {
        Resolves(Association(null, null, Now, anglers.Length == 0 ? [Me] : anglers));
        await Sut.InitializeNewAsync(ViewerId, null, CancellationToken.None);
    }

    [Fact]
    public async Task ItShouldNotLeaveWhenThereIsNoTripOrTheRecordIsLocked()
    {
        // Arrange
        Resolves(null);
        await Sut.InitializeNewAsync(ViewerId, null, CancellationToken.None);
        // Act
        Sut.Leave();
        // Assert
        SelectionChanges.Should().Be(0);
    }

    [Fact]
    public async Task ItShouldLeaveTheTripAndTellTheDraftToSave()
    {
        // Arrange
        await JoinAsync();
        // Act
        Sut.Leave();
        // Assert
        Sut.ShowAssociation.Should().BeFalse();
        Sut.ShowStandalone.Should().BeTrue();
        Sut.CaughtByUserId.Should().Be(ViewerId);
        SelectionChanges.Should().Be(1);
    }

    [Fact]
    public async Task ItShouldRejoinTheTripItLeftAndTellTheDraftToSave()
    {
        // Arrange
        await JoinAsync();
        Sut.Leave();
        // Act
        Sut.Rejoin();
        // Assert
        Sut.ShowAssociation.Should().BeTrue();
        Sut.TripId.Should().Be(TripId);
        SelectionChanges.Should().Be(2);
    }

    [Fact]
    public async Task ItShouldNotRejoinWhenThereIsNothingToRejoin()
    {
        // Arrange
        await JoinAsync();
        // Act
        Sut.Rejoin();
        // Assert
        Sut.ShowAssociation.Should().BeTrue();
        SelectionChanges.Should().Be(0);
    }

    [Fact]
    public async Task ItShouldOfferOnlyMeWhenTheTripHasNoOtherAnglers()
    {
        // Arrange
        await JoinAsync();
        // Act
        var options = Sut.AnglerOptions;
        // Assert
        options.Should().ContainSingle().Which.Should().Match<FishingLogBook.Mobile.Features.Catch.Models.CatchChipOptionModel>(option => option.Code == "me" && option.IsSelected);
    }

    [Fact]
    public async Task ItShouldOfferNoAnglersWhenThereIsNoTrip()
    {
        // Arrange
        Resolves(null);
        await Sut.InitializeNewAsync(ViewerId, null, CancellationToken.None);
        // Act
        var options = Sut.AnglerOptions;
        // Assert
        options.Should().ContainSingle().Which.Name.Should().Be(Text["CatchAnglerMe"]);
    }

    [Fact]
    public async Task ItShouldLabelEveryAnglerOnASharedTripWithMeNamesOrUnknown()
    {
        // Arrange
        await JoinAsync(Me, Sam, new TripAnglerOptionModel(Guid.NewGuid(), null, false));
        // Act
        var options = Sut.AnglerOptions;
        // Assert
        options.Select(option => option.Name).Should().Equal(Text["CatchAnglerMe"], "Sam", Text["CatchTripAnglerUnknown"]);
        options.Select(option => option.IsSelected).Should().Equal(true, false, false);
    }

    [Fact]
    public async Task ItShouldSelectAnotherAnglerOnTheTripOnce()
    {
        // Arrange
        await JoinAsync(Me, Sam);
        // Act
        Sut.SelectAngler(FriendId.ToString("D"));
        Sut.SelectAngler(FriendId.ToString("D"));
        // Assert
        Sut.CaughtByUserId.Should().Be(FriendId);
        SelectionChanges.Should().Be(1);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("99999999-9999-9999-9999-999999999999")]
    public async Task ItShouldIgnoreAnAnglerCodeThatIsNotOnTheTrip(string code)
    {
        // Arrange
        await JoinAsync(Me, Sam);
        // Act
        Sut.SelectAngler(code);
        // Assert
        Sut.CaughtByUserId.Should().Be(ViewerId);
        SelectionChanges.Should().Be(0);
    }

    [Fact]
    public async Task ItShouldNotSelectAnAnglerWhenThereIsNoTrip()
    {
        // Arrange
        Resolves(null);
        await Sut.InitializeNewAsync(ViewerId, null, CancellationToken.None);
        // Act
        Sut.SelectAngler(FriendId.ToString("D"));
        // Assert
        SelectionChanges.Should().Be(0);
    }

    [Fact]
    public async Task ItShouldAlwaysBeAvailableWhenThereIsNoTripOrTheRecordIsLocked()
    {
        // Arrange
        Resolves(null);
        await Sut.InitializeNewAsync(ViewerId, null, CancellationToken.None);
        // Act
        var available = await Sut.ConfirmAvailableAsync(CancellationToken.None);
        // Assert
        available.Should().BeTrue();
        await MockService.DidNotReceive().IsAvailableAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ItShouldFlagTheTripUnavailableWhenTheServiceSaysItCannotBeUsed(bool usable)
    {
        // Arrange
        await JoinAsync();
        MockService.IsAvailableAsync(TripId, Arg.Any<CancellationToken>()).Returns(usable);
        // Act
        var available = await Sut.ConfirmAvailableAsync(CancellationToken.None);
        // Assert
        available.Should().Be(usable);
        Sut.IsUnavailable.Should().Be(!usable);
    }

    [Fact]
    public async Task ItShouldNameTheTripByTitleOtherwiseByStartTimeAndPlace()
    {
        // Arrange
        Resolves(Association("Corrib"));
        await Sut.InitializeNewAsync(ViewerId, null, CancellationToken.None);
        var titled = Sut.TripName;
        Resolves(Association(null, "Lough Mask", Now));
        await Sut.InitializeNewAsync(ViewerId, null, CancellationToken.None);
        var withPlace = Sut.TripName;
        Resolves(Association(null, null, Now));
        await Sut.InitializeNewAsync(ViewerId, null, CancellationToken.None);
        var startOnly = Sut.TripName;
        // Act
        // Assert
        titled.Should().Be("Corrib");
        withPlace.Should().EndWith("· Lough Mask");
        startOnly.Should().NotBeEmpty().And.NotContain("·");
    }
}
