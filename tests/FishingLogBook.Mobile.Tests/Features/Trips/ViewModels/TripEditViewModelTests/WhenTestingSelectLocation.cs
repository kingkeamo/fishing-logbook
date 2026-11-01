using AwesomeAssertions;
using static FishingLogBook.Mobile.Tests.Features.Trips.TestSupport.TripTestData;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.TripEditViewModelTests;

public sealed class WhenTestingSelectLocation : BaseTripEditViewModelTest
{
    [Fact]
    public async Task ItShouldTrimTheChosenLocationAndSelectItsChip()
    {
        // Arrange
        ServeSavedLocations("Lough Corrib", "River Shannon");
        await LoadAsync(Trip(place: "Lough Corrib"));
        // Act
        Sut.SelectLocation("  River Shannon ");
        // Assert
        Sut.EditPlace.Should().Be("River Shannon");
        Sut.LocationOptions.Select(option => (option.Name, option.IsSelected)).Should().Equal(("Lough Corrib", false), ("River Shannon", true));
        Sut.HasEditPlace.Should().BeTrue();
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("")]
    public async Task ItShouldClearThePlaceWhenTheChosenLocationIsBlank(string name)
    {
        // Arrange
        await LoadAsync(Trip(place: "Lough Corrib"));
        // Act
        Sut.SelectLocation(name);
        // Assert
        Sut.EditPlace.Should().BeEmpty();
        Sut.HasEditPlace.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldAcceptALocationAtTheMaximumLengthAndRejectOneCharacterMore()
    {
        // Arrange
        await LoadAsync(Trip());
        var atLimit = new string('a', 160);
        // Act
        Sut.SelectLocation(atLimit);
        var accepted = Sut.EditPlace;
        Sut.SelectLocation(atLimit + "a");
        // Assert
        accepted.Should().Be(atLimit);
        Sut.EditPlace.Should().BeEmpty();
    }
}
