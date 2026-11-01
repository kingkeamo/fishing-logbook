using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Features.Trips.Models;
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
        MockPlanned.GetEligibleAsync(Arg.Any<CancellationToken>()).Returns([option]);
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
    public async Task ItShouldStartANewTripWithoutAChooserWhenTheEligibilityReadFails()
    {
        // Arrange
        MockPlanned.GetEligibleAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException("down"));

        // Act
        var id = await Sut.StartAsync(CancellationToken.None);

        // Assert
        id.Should().NotBeNull();
        await MockChooser.DidNotReceive().ChoosePlannedTripAsync(Arg.Any<IReadOnlyList<PlannedTripOptionModel>>());
        await MockTrips.Received(1).StartAsync(null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldDoNothingWhenTheChooserIsDismissed()
    {
        // Arrange
        MockPlanned.GetEligibleAsync(Arg.Any<CancellationToken>()).Returns([Option()]);
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
        MockPlanned.GetEligibleAsync(Arg.Any<CancellationToken>()).Returns([option]);
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
        MockPlanned.GetEligibleAsync(Arg.Any<CancellationToken>()).Returns([option]);
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
