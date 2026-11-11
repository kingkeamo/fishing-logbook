using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Settings.Models;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Settings.ViewModels.LanguageSelectorViewModelTests;

public class WhenTestingSelect : BaseLanguageSelectorViewModelTest
{
    [Fact]
    public void ItShouldRejectAnUnknownOptionWithoutClosingOrChangingSelection()
    {
        // Arrange
        var sut = CreateSut();
        sut.Open();
        var select = () => sut.Select("unsupported");
        // Act
        select.Should().Throw<ArgumentOutOfRangeException>();
        // Assert
        sut.IsOpen.Should().BeTrue();
        sut.SelectedLanguage.CultureName.Should().Be("en-GB");
        MockSelection.DidNotReceive().Invoke(Arg.Any<string>());
    }

    [Fact]
    public void ItShouldKeepTheMenuOpenAndShowAnErrorWhenSavingFails()
    {
        // Arrange
        var sut = CreateSut();
        sut.Open();
        MockSelection.When(callback => callback("fr-FR")).Do(_ => throw new IOException("Unavailable"));
        // Act
        var result = sut.Select("fr-FR");
        // Assert
        result.Should().BeFalse();
        sut.IsOpen.Should().BeTrue();
        sut.SelectedLanguage.CultureName.Should().Be("en-GB");
        sut.HasStatus.Should().BeTrue();
        sut.StatusText.Should().Be(Text["SettingsSaveFailed"]);
        MockSelection.Received(1).Invoke("fr-FR");
    }

    [Fact]
    public void ItShouldCloseAnUnchangedSelectionWithoutPersistingAgain()
    {
        // Arrange
        var sut = CreateSut();
        sut.Open();
        // Act
        var result = sut.Select("en-GB");
        // Assert
        result.Should().BeTrue();
        sut.IsOpen.Should().BeFalse();
        sut.HasStatus.Should().BeFalse();
        MockSelection.DidNotReceive().Invoke(Arg.Any<string>());
    }

    [Fact]
    public void ItShouldSupportAnotherLanguageThroughDataWithoutNewSelectionLogic()
    {
        // Arrange
        var sut = CreateSut(languages: [.. Languages, new LanguageOptionModel("es-ES", "Español", "flag_es.png")]);
        sut.Open();
        // Act
        var result = sut.Select("es-ES");
        // Assert
        result.Should().BeTrue();
        sut.Options.Should().HaveCount(3);
        sut.SelectedLanguage.Name.Should().Be("Español");
        sut.SelectedLanguage.FlagSource.Should().Be("flag_es.png");
        sut.Options.Count(option => option.IsSelected).Should().Be(1);
        sut.IsOpen.Should().BeFalse();
        MockSelection.Received(1).Invoke("es-ES");
    }

    [Theory]
    [InlineData("en-GB", "fr-FR", "Français")]
    [InlineData("fr-FR", "en-GB", "English")]
    public void ItShouldSaveOnceUpdateTheCheckmarkAndClose(string initial, string selected, string name)
    {
        // Arrange
        var sut = CreateSut(initial);
        sut.Open();
        // Act
        var result = sut.Select(selected);
        // Assert
        result.Should().BeTrue();
        sut.IsOpen.Should().BeFalse();
        sut.SelectedLanguage.CultureName.Should().Be(selected);
        sut.Options.Single(option => option.IsSelected).Name.Should().Be(name);
        sut.SelectedLanguage.AccessibilityDescription.Should().Be($"{name}, selected");
        sut.Options.Single(option => !option.IsSelected).AccessibilityDescription.Should().EndWith(", not selected");
        MockSelection.Received(1).Invoke(selected);
    }
}
