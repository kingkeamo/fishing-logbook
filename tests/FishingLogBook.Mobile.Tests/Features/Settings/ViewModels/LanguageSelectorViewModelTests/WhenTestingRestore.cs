using AwesomeAssertions;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Settings.ViewModels.LanguageSelectorViewModelTests;

public class WhenTestingRestore : BaseLanguageSelectorViewModelTest
{
    [Fact]
    public void ItShouldRestoreSelectionAndLocalizedAccessibilityWithoutPersisting()
    {
        // Arrange
        var sut = CreateSut();
        Text.SetCulture("fr-FR");
        // Act
        sut.Restore("fr-FR");
        // Assert
        sut.SelectedLanguage.CultureName.Should().Be("fr-FR");
        sut.SelectedLanguage.AccessibilityDescription.Should().Be("Français, sélectionné");
        sut.AccessibilityDescription.Should().Be("Langue : Français, réduit");
        MockSelection.DidNotReceive().Invoke(Arg.Any<string>());
    }
}
