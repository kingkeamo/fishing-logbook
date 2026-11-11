using AwesomeAssertions;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Settings.ViewModels.LanguageSelectorViewModelTests;

public class WhenTestingClose : BaseLanguageSelectorViewModelTest
{
    [Fact]
    public void ItShouldDismissWithoutChangingTheLanguageAndIgnoreRepeatedDismissals()
    {
        // Arrange
        var sut = CreateSut("fr-FR");
        sut.Open();
        var changes = new List<string?>();
        sut.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
        // Act
        sut.Close();
        sut.Close();
        // Assert
        sut.IsOpen.Should().BeFalse();
        sut.SelectedLanguage.CultureName.Should().Be("fr-FR");
        sut.AccessibilityDescription.Should().Be("Language: Français, collapsed");
        changes.Count(property => property == nameof(sut.IsOpen)).Should().Be(1);
        MockSelection.DidNotReceive().Invoke(Arg.Any<string>());
    }
}
