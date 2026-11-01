using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using FishingLogBook.Shared.Dtos;
using NSubstitute;
using static FishingLogBook.Mobile.Tests.Features.Trips.TestSupport.TripTestData;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.ActiveTripBannerViewModelTests;

public sealed class WhenTestingNavigation : BaseActiveTripBannerViewModelTest
{
    [Theory]
    [InlineData("en-GB")]
    [InlineData("fr-FR")]
    public async Task ItShouldShowALocalizedActivationFailureAndAllowRetry(string culture)
    {
        // Arrange
        Text.SetCulture(culture);
        ServeTrips();
        await Sut.RefreshAsync(true, CancellationToken.None);
        MockPlanned.GetEligibleAsync(Arg.Any<CancellationToken>()).Returns(FishingLogBook.Mobile.Features.Trips.Models.PlannedTripLookupModel.Succeeded([
            new FishingLogBook.Mobile.Features.Trips.Models.PlannedTripOptionModel(TripId, Now, ViewerId, null, true)]));
        MockChooser.ChoosePlannedTripAsync(Arg.Any<IReadOnlyList<FishingLogBook.Mobile.Features.Trips.Models.PlannedTripOptionModel>>())
            .Returns(new FishingLogBook.Mobile.Features.Trips.Models.PlannedTripChoiceModel(new FishingLogBook.Mobile.Features.Trips.Models.PlannedTripOptionModel(TripId, Now, ViewerId, null, true), false));
        MockPlanned.ActivateAsync(Arg.Any<FishingLogBook.Mobile.Features.Trips.Models.PlannedTripOptionModel>(), Arg.Any<CancellationToken>())
            .Returns<Task<Guid>>(_ => throw new InvalidOperationException());

        // Act
        await Sut.StartAndOpenAsync(CancellationToken.None);

        // Assert
        Sut.HasError.Should().BeTrue();
        Sut.ErrorText.Should().Be(Text["TripPlannedStartFailed"]);
        Sut.CanStart.Should().BeTrue();
        await MockTrips.DidNotReceive().StartAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await MockNavigator.DidNotReceive().GoToAsync(Arg.Any<string>(), Arg.Any<FishingLogBook.Mobile.Features.Catch.Models.CatchPresentationModel?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }
    [Fact]
    public async Task ItShouldNotNavigateWhenThereIsNoActiveTrip()
    {
        // Arrange
        ServeTrips();
        await Sut.RefreshAsync(force: false, CancellationToken.None);
        // Act
        await Sut.ViewTripAsync();
        await Sut.UpdateTripAsync();
        // Assert
        await MockNavigator.DidNotReceive().GoToAsync(Arg.Any<string>(), Arg.Any<FishingLogBook.Mobile.Features.Catch.Models.CatchPresentationModel?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldOpenTheActiveTripToViewIt()
    {
        // Arrange
        ServeTrips(Summary(ActiveId));
        await Sut.RefreshAsync(force: false, CancellationToken.None);
        // Act
        await Sut.ViewTripAsync();
        // Assert
        await MockNavigator.Received(1).GoToAsync("trip-detail", null, null, ActiveId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldOpenTheActiveTripToUpdateIt()
    {
        // Arrange
        ServeTrips(Summary(ActiveId));
        await Sut.RefreshAsync(force: false, CancellationToken.None);
        // Act
        await Sut.UpdateTripAsync();
        // Assert
        await MockNavigator.Received(1).GoToAsync("trip-edit", null, null, ActiveId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotStartAnotherTripWhenOneIsAlreadyActive()
    {
        // Arrange
        ServeTrips(Summary(ActiveId));
        await Sut.RefreshAsync(force: false, CancellationToken.None);
        // Act
        await Sut.StartAndOpenAsync(CancellationToken.None);
        // Assert
        await MockTrips.DidNotReceive().StartAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await MockNavigator.DidNotReceive().GoToAsync(Arg.Any<string>(), Arg.Any<FishingLogBook.Mobile.Features.Catch.Models.CatchPresentationModel?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStartATripAtTheDefaultLocationAndOpenItWhenThereIsNoActiveTrip()
    {
        // Arrange
        ServeTrips();
        await Sut.RefreshAsync(force: false, CancellationToken.None);
        MockPreferences.GetCachedAsync(Arg.Any<CancellationToken>()).Returns(FishingLogBook.Mobile.Features.Profile.Models.AnglerPreferencesModel.Empty with
        {
            Locations = new FishingLocationPreferencesDto([new FishingLocationPreferenceDto(Guid.NewGuid(), "Lough Corrib", true)])
        });
        MockTrips.StartAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(Trip(place: "Lough Corrib"));
        // Act
        await Sut.StartAndOpenAsync(CancellationToken.None);
        // Assert
        await MockTrips.Received(1).StartAsync(null, "Lough Corrib", Arg.Any<CancellationToken>());
        await MockNavigator.Received(1).GoToAsync("trip-detail", null, null, TripId, Arg.Any<CancellationToken>());
        Sut.HasActiveTrip.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldNotNavigateWhenStartingAFreshTripFailsBecauseOneIsAlreadyActive()
    {
        // Arrange
        ServeTrips();
        await Sut.RefreshAsync(force: false, CancellationToken.None);
        MockTrips.StartAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns<LocalTripRecordModel>(_ => throw new InvalidOperationException());
        // Act
        await Sut.StartAndOpenAsync(CancellationToken.None);
        // Assert
        await MockNavigator.DidNotReceive().GoToAsync(Arg.Any<string>(), Arg.Any<FishingLogBook.Mobile.Features.Catch.Models.CatchPresentationModel?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }
}
