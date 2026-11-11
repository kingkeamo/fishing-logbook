using AwesomeAssertions;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Settings.ViewModels.LanguageSelectorViewModelTests;

public class WhenTestingConstructor : BaseLanguageSelectorViewModelTest
{
    [Fact]
    public void ItShouldRejectEmptyOrDuplicateOptions()
    {
        // Arrange
        var empty = () => CreateSut(languages: []);
        var duplicate = () => CreateSut(languages: [Languages[0], Languages[0]]);
        // Act
        empty.Should().Throw<ArgumentException>();
        duplicate.Should().Throw<ArgumentException>();
        // Assert
        MockSelection.DidNotReceive().Invoke(Arg.Any<string>());
    }

    [Theory]
    [InlineData("en-GB", "English", "flag_gb.png")]
    [InlineData("fr-FR", "Français", "flag_fr.png")]
    public void ItShouldDisplayTheSelectedNativeNameAndDecorativeFlag(string culture, string name, string flag)
    {
        // Arrange
        var sut = CreateSut(culture);
        // Act
        var selected = sut.SelectedLanguage;
        // Assert
        selected.CultureName.Should().Be(culture);
        selected.Name.Should().Be(name);
        selected.FlagSource.Should().Be(flag);
        selected.IsSelected.Should().BeTrue();
        selected.AutomationId.Should().Be($"Language{culture}");
        sut.Options.Select(option => option.Name).Should().Equal("English", "Français");
        sut.Options.Count(option => option.IsSelected).Should().Be(1);
        sut.AccessibilityDescription.Should().Be($"Language: {name}, collapsed");
        sut.IsOpen.Should().BeFalse();
        MockSelection.DidNotReceive().Invoke(Arg.Any<string>());
    }
}
