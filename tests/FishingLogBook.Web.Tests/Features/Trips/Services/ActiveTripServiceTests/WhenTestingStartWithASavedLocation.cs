using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Profile.Models;
using FishingLogBook.Web.Features.Trips.Models;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.Trips.Services.ActiveTripServiceTests;

public class WhenTestingStartWithASavedLocation : BaseActiveTripServiceTest
{
    private static readonly Guid CorribId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid MoyId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");

    [Fact]
    public async Task ItShouldStartWithNoPlaceWhenReadingThePreferencesFails()
    {
        // Arrange
        MockAnglerPreferences.GetAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("preferences unavailable"));

        // Act
        var started = await Sut.StartAsync(OwnerUserId, CancellationToken.None);

        // Assert
        started.PlaceName.Should().BeNull();
        started.Status.Should().Be(TripConstants.Active);
        await MockTripStore.Received(1).SaveAsync(
            Arg.Is<TripModel>(trip => trip.PlaceName == null),
            Arg.Any<CancellationToken>());
        await MockLogging.Received(1).LogErrorAsync(
            "resolving the default fishing location",
            Arg.Any<InvalidOperationException>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStartWithNoPlaceWhenNoLocationsAreSaved()
    {
        // Arrange
        GivenSavedLocations();

        // Act
        var started = await Sut.StartAsync(OwnerUserId, CancellationToken.None);

        // Assert
        started.PlaceName.Should().BeNull();
        await MockTripStore.Received(1).SaveAsync(
            Arg.Is<TripModel>(trip => trip.PlaceName == null),
            Arg.Any<CancellationToken>());
        await MockAnglerPreferences.Received(1).GetAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStartWithNoPlaceWhenNoSavedLocationIsTheDefault()
    {
        // Arrange
        GivenSavedLocations(
            new FishingLocationPreferenceDto(CorribId, "Lough Corrib", false),
            new FishingLocationPreferenceDto(MoyId, "River Moy", false));

        // Act
        var started = await Sut.StartAsync(OwnerUserId, CancellationToken.None);

        // Assert
        started.PlaceName.Should().BeNull();
        await MockTripStore.Received(1).SaveAsync(
            Arg.Is<TripModel>(trip => trip.PlaceName == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldIgnoreADefaultLocationNameThatIsTooLongForATrip()
    {
        // Arrange
        var tooLong = new string('a', TripConstants.MaxPlaceNameLength + 1);
        GivenSavedLocations(new FishingLocationPreferenceDto(CorribId, tooLong, true));

        // Act
        var started = await Sut.StartAsync(OwnerUserId, CancellationToken.None);

        // Assert
        started.PlaceName.Should().BeNull();
        await MockTripStore.Received(1).SaveAsync(
            Arg.Is<TripModel>(trip => trip.PlaceName == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldCopyTheDefaultSavedLocationOntoTheNewTrip()
    {
        // Arrange
        GivenSavedLocations(
            new FishingLocationPreferenceDto(MoyId, "River Moy", false),
            new FishingLocationPreferenceDto(CorribId, "Lough Corrib", true));

        // Act
        var started = await Sut.StartAsync(OwnerUserId, CancellationToken.None);

        // Assert
        started.PlaceName.Should().Be("Lough Corrib");
        started.Location.Should().BeNull();
        await MockTripStore.Received(1).SaveAsync(
            Arg.Is<TripModel>(trip =>
                trip.PlaceName == "Lough Corrib" &&
                trip.OwnerUserId == OwnerUserId &&
                trip.Status == TripConstants.Active),
            Arg.Any<CancellationToken>());
        await MockAnglerPreferences.Received(1).GetAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldTrimTheDefaultSavedLocationName()
    {
        // Arrange
        GivenSavedLocations(new FishingLocationPreferenceDto(CorribId, "  Lough Corrib  ", true));

        // Act
        var started = await Sut.StartAsync(OwnerUserId, CancellationToken.None);

        // Assert
        started.PlaceName.Should().Be("Lough Corrib");
        await MockTripStore.Received(1).SaveAsync(
            Arg.Is<TripModel>(trip => trip.PlaceName == "Lough Corrib"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldOnlyReadTheSavedLocationsWhenStartingATrip()
    {
        // Arrange
        GivenSavedLocations(new FishingLocationPreferenceDto(CorribId, "Lough Corrib", true));

        // Act
        await Sut.StartAsync(OwnerUserId, CancellationToken.None);

        // Assert
        await MockAnglerPreferences.Received(1).GetAsync(Arg.Any<CancellationToken>());
        await MockAnglerPreferences.DidNotReceive().SetAsync(
            Arg.Any<Guid>(),
            Arg.Any<AnglerPreferencesModel>(),
            Arg.Any<CancellationToken>());
    }

    private void GivenSavedLocations(params FishingLocationPreferenceDto[] locations)
    {
        MockAnglerPreferences.GetAsync(Arg.Any<CancellationToken>())
            .Returns(AnglerPreferencesModel.Empty with { Locations = locations });
    }
}
