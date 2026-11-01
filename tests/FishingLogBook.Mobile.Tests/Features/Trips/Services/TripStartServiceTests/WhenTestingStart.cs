using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Features.Trips.Models;
using FishingLogBook.Mobile.Interactions;
using FishingLogBook.Shared.Dtos;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Mobile.Tests.Features.Trips.Services.TripStartServiceTests;

public sealed class WhenTestingStart : BaseTripStartServiceTest
{
    [Fact]
    public async Task ItShouldPropagateAFailureToActivateThePlannedTrip()
    {
        // Arrange
        var option = Option();
        MockPlanned.GetEligibleAsync(Arg.Any<CancellationToken>()).Returns(Found(option));
        MockChooser.ChoosePlannedTripAsync(Arg.Any<IReadOnlyList<PlannedTripOptionModel>>())
            .Returns(new PlannedTripChoiceModel(option, IsDismissed: false));
        MockPlanned.ActivateAsync(option, Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("active"));

        // Act
        var act = () => Sut.StartAsync(CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        await MockTrips.DidNotReceive().StartAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotStartANewTripWhenTheLookupFailsAndTheRetryIsDeclined()
    {
        // Arrange
        MockPlanned.GetEligibleAsync(Arg.Any<CancellationToken>()).Returns(PlannedTripLookupModel.Unavailable([]));

        // Act
        var id = await Sut.StartAsync(CancellationToken.None);

        // Assert
        id.Should().BeNull();
        await MockPresenter.Received(1).ShowDialogAsync(
            Arg.Is<DialogRequest>(request => request.Title == "Planned trips unavailable"
                && request.Accept == "Try again"
                && request.Cancel == "Cancel"),
            Arg.Any<CancellationToken>());
        await MockTrips.DidNotReceive().StartAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await MockChooser.DidNotReceive().ChoosePlannedTripAsync(Arg.Any<IReadOnlyList<PlannedTripOptionModel>>());
        await MockPlanned.Received(1).GetEligibleAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStartANewTripWhenARetrySucceedsWithNoPlannedTrips()
    {
        // Arrange
        MockPlanned.GetEligibleAsync(Arg.Any<CancellationToken>())
            .Returns(PlannedTripLookupModel.Unavailable([]), PlannedTripLookupModel.Succeeded([]));
        MockPresenter.ShowDialogAsync(Arg.Any<DialogRequest>(), Arg.Any<CancellationToken>()).Returns(true);

        // Act
        var id = await Sut.StartAsync(CancellationToken.None);

        // Assert
        id.Should().NotBeNull();
        await MockPlanned.Received(2).GetEligibleAsync(Arg.Any<CancellationToken>());
        await MockPresenter.Received(1).ShowDialogAsync(Arg.Any<DialogRequest>(), Arg.Any<CancellationToken>());
        await MockTrips.Received(1).StartAsync(null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldOfferThePlannedTripsWhenARetrySucceeds()
    {
        // Arrange
        var option = Option();
        MockPlanned.GetEligibleAsync(Arg.Any<CancellationToken>())
            .Returns(PlannedTripLookupModel.Unavailable([]), Found(option));
        MockPresenter.ShowDialogAsync(Arg.Any<DialogRequest>(), Arg.Any<CancellationToken>()).Returns(true);
        MockChooser.ChoosePlannedTripAsync(Arg.Any<IReadOnlyList<PlannedTripOptionModel>>())
            .Returns(new PlannedTripChoiceModel(option, IsDismissed: false));
        MockPlanned.ActivateAsync(option, Arg.Any<CancellationToken>()).Returns(PlannedTripId);

        // Act
        var id = await Sut.StartAsync(CancellationToken.None);

        // Assert
        id.Should().Be(PlannedTripId);
        await MockTrips.DidNotReceive().StartAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepAskingToRetryWhileTheLookupKeepsFailing()
    {
        // Arrange
        MockPlanned.GetEligibleAsync(Arg.Any<CancellationToken>()).Returns(PlannedTripLookupModel.Unavailable([]));
        MockPresenter.ShowDialogAsync(Arg.Any<DialogRequest>(), Arg.Any<CancellationToken>()).Returns(true, true, false);

        // Act
        var id = await Sut.StartAsync(CancellationToken.None);

        // Assert
        id.Should().BeNull();
        await MockPlanned.Received(3).GetEligibleAsync(Arg.Any<CancellationToken>());
        await MockPresenter.Received(3).ShowDialogAsync(Arg.Any<DialogRequest>(), Arg.Any<CancellationToken>());
        await MockTrips.DidNotReceive().StartAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotPromptWhenTheLookupSucceedsWithNothingPlanned()
    {
        // Arrange
        // Act
        var id = await Sut.StartAsync(CancellationToken.None);

        // Assert
        id.Should().NotBeNull();
        await MockPresenter.DidNotReceive().ShowDialogAsync(Arg.Any<DialogRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldFollowTheOfflineLookupResultWithoutPrompting()
    {
        // Arrange
        var option = Option();
        MockPlanned.GetEligibleAsync(Arg.Any<CancellationToken>()).Returns(Found(option));
        MockChooser.ChoosePlannedTripAsync(Arg.Any<IReadOnlyList<PlannedTripOptionModel>>())
            .Returns(PlannedTripChoiceModel.StartNew);

        // Act
        var id = await Sut.StartAsync(CancellationToken.None);

        // Assert
        id.Should().NotBeNull();
        await MockPresenter.DidNotReceive().ShowDialogAsync(Arg.Any<DialogRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldDoNothingWhenTheChooserIsDismissed()
    {
        // Arrange
        MockPlanned.GetEligibleAsync(Arg.Any<CancellationToken>()).Returns(Found(Option()));
        MockChooser.ChoosePlannedTripAsync(Arg.Any<IReadOnlyList<PlannedTripOptionModel>>())
            .Returns(PlannedTripChoiceModel.Dismissed);

        // Act
        var id = await Sut.StartAsync(CancellationToken.None);

        // Assert
        id.Should().BeNull();
        await MockTrips.DidNotReceive().StartAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await MockPlanned.DidNotReceive().ActivateAsync(Arg.Any<PlannedTripOptionModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStartANewTripWithTheDefaultLocationWhenStartNewTripIsChosen()
    {
        // Arrange
        var option = Option();
        MockPlanned.GetEligibleAsync(Arg.Any<CancellationToken>()).Returns(Found(option));
        MockChooser.ChoosePlannedTripAsync(Arg.Is<IReadOnlyList<PlannedTripOptionModel>>(options => options.Count == 1 && options[0] == option))
            .Returns(PlannedTripChoiceModel.StartNew);
        MockPreferences.GetCachedAsync(Arg.Any<CancellationToken>()).Returns(AnglerPreferencesModel.Empty with
        {
            Locations = new FishingLocationPreferencesDto([new FishingLocationPreferenceDto(Guid.NewGuid(), "Lough Corrib", IsDefault: true)])
        });

        // Act
        var id = await Sut.StartAsync(CancellationToken.None);

        // Assert
        id.Should().NotBeNull();
        await MockTrips.Received(1).StartAsync(null, "Lough Corrib", Arg.Any<CancellationToken>());
        await MockPlanned.DidNotReceive().ActivateAsync(Arg.Any<PlannedTripOptionModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldActivateTheChosenPlannedTripInsteadOfStartingANewOne()
    {
        // Arrange
        var option = Option();
        MockPlanned.GetEligibleAsync(Arg.Any<CancellationToken>()).Returns(Found(option));
        MockChooser.ChoosePlannedTripAsync(Arg.Any<IReadOnlyList<PlannedTripOptionModel>>())
            .Returns(new PlannedTripChoiceModel(option, IsDismissed: false));
        MockPlanned.ActivateAsync(option, Arg.Any<CancellationToken>()).Returns(PlannedTripId);

        // Act
        var id = await Sut.StartAsync(CancellationToken.None);

        // Assert
        id.Should().Be(PlannedTripId);
        await MockPlanned.Received(1).ActivateAsync(option, Arg.Any<CancellationToken>());
        await MockTrips.DidNotReceive().StartAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStartANewTripWithoutAChooserWhenNothingIsPlanned()
    {
        // Arrange
        // Act
        var id = await Sut.StartAsync(CancellationToken.None);

        // Assert
        id.Should().NotBeNull();
        await MockChooser.DidNotReceive().ChoosePlannedTripAsync(Arg.Any<IReadOnlyList<PlannedTripOptionModel>>());
        await MockTrips.Received(1).StartAsync(null, null, Arg.Any<CancellationToken>());
    }
}
