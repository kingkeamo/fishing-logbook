using AwesomeAssertions;

namespace FishingLogBook.Mobile.Tests.Features.Catch.ViewModels.CatchListViewModelTests;

public class WhenTestingToggleEmpty : BaseCatchListViewModelTest
{
    [Fact]
    public void ItShouldKeepEmptyStateAcrossLanguageChangeAndRestoreBothOriginalSamples()
    {
        // Arrange
        var ids = Sut.Catches.Select(item => item.Id).ToArray();

        // Act
        Sut.ToggleEmpty();
        Text.SetCulture("fr-FR");

        // Assert
        Sut.Catches.Should().BeEmpty();
        Sut.EmptyToggleText.Should().Be("Rétablir les prises fictives");

        // Act
        Sut.ToggleEmpty();

        // Assert
        Sut.Catches.Select(item => item.Id).Should().Equal(ids);
        Sut.Catches[0].HasPhoto.Should().BeTrue();
        Sut.Catches[1].HasPhoto.Should().BeFalse();
        Sut.EmptyToggleText.Should().Be("Afficher la liste vide");
    }
}
