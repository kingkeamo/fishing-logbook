using AwesomeAssertions;
using static FishingLogBook.Mobile.Tests.Features.Trips.TestSupport.TripTestData;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.TripEditViewModelTests;

public sealed class WhenTestingLocation : BaseTripEditViewModelTest
{
    [Fact]
    public async Task ItShouldOfferNoChoicesWhenTheAnglerHasNoSavedLocations()
    {
        // Arrange
        // Act
        await LoadAsync(Trip(place: "Lough Corrib"));
        // Assert
        Sut.LocationOptions.Should().BeEmpty();
        Sut.HasLocationOptions.Should().BeFalse();
        Sut.EditPlace.Should().Be("Lough Corrib");
    }

    [Fact]
    public async Task ItShouldListSavedLocationsAndSelectTheOneMatchingTheTripPlaceIgnoringCaseAndSpaces()
    {
        // Arrange
        ServeSavedLocations("Lough Corrib", "River Shannon");
        // Act
        await LoadAsync(Trip(place: "lough corrib "));
        // Assert
        Sut.LocationOptions.Select(option => (option.Name, option.IsSelected)).Should().Equal(("Lough Corrib", true), ("River Shannon", false));
        Sut.HasLocationOptions.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldMoveTheSelectionWhenAnotherSavedLocationIsChosen()
    {
        // Arrange
        ServeSavedLocations("Lough Corrib", "River Shannon");
        await LoadAsync(Trip(place: "Lough Corrib"));
        // Act
        Sut.EditPlace = "River Shannon";
        // Assert
        Sut.LocationOptions.Select(option => option.IsSelected).Should().Equal(false, true);
    }

    [Fact]
    public async Task ItShouldDeselectEverySavedLocationWhenAnOtherLocationIsTyped()
    {
        // Arrange
        ServeSavedLocations("Lough Corrib", "River Shannon");
        await LoadAsync(Trip(place: "Lough Corrib"));
        // Act
        Sut.EditPlace = "Lough Mask";
        // Assert
        Sut.LocationOptions.Select(option => option.IsSelected).Should().Equal(false, false);
        Sut.HasEditPlace.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldDeselectEverySavedLocationAndHideClearWhenTheLocationIsCleared()
    {
        // Arrange
        ServeSavedLocations("Lough Corrib");
        await LoadAsync(Trip(place: "Lough Corrib"));
        // Act
        Sut.EditPlace = string.Empty;
        // Assert
        Sut.LocationOptions.Should().ContainSingle().Which.IsSelected.Should().BeFalse();
        Sut.HasEditPlace.Should().BeFalse();
    }
}
