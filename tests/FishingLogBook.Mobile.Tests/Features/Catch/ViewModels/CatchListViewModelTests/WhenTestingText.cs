using AwesomeAssertions;

namespace FishingLogBook.Mobile.Tests.Features.Catch.ViewModels.CatchListViewModelTests;

public class WhenTestingText : BaseCatchListViewModelTest
{
    [Fact]
    public void ItShouldRefreshExistingSamplesAndPickerLabelsWhenLanguageChanges()
    {
        // Arrange
        var ids = Sut.Catches.Select(item => item.Id).ToArray();
        var notifications = new List<string?>();
        Sut.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        // Act
        Sut.Text.SetCulture("fr-CA");

        // Assert
        Sut.Catches.Select(item => item.Id).Should().Equal(ids);
        Sut.Catches[0].Species.Should().Be("Perche commune");
        Sut.Catches[0].Weight.Should().Be("2,4 kg");
        Sut.Catches[1].Species.Should().Be("Non renseigné");
        Sut.Themes.Should().Equal("Suivre l’appareil", "Clair", "Sombre");
        notifications.Should().Contain(nameof(Sut.Catches)).And.Contain(nameof(Sut.Themes));
    }
}
