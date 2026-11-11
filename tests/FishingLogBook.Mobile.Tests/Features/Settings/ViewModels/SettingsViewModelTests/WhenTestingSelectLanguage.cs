using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Settings.Services;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Settings.ViewModels.SettingsViewModelTests;

public class WhenTestingSelectLanguage : BaseSettingsViewModelTest
{
    [Fact]
    public void ItShouldKeepTheCurrentLanguageAndThemeWhenSavingTheLanguageFails()
    {
        // Arrange
        using var sut = CreateSut();
        sut.SelectAppearance(AppearanceEnum.Dark);
        MockStore.ClearReceivedCalls();
        MockAppearance.ClearReceivedCalls();
        MockStore.When(store => store.Set(PreferenceService.LanguageKey, "fr-FR"))
            .Do(_ => throw new IOException("Unavailable"));
        sut.Language.Open();
        // Act
        var result = sut.Language.Select("fr-FR");
        // Assert
        result.Should().BeFalse();
        sut.Language.IsOpen.Should().BeTrue();
        sut.Language.StatusText.Should().Be(Text["SettingsSaveFailed"]);
        sut.Language.SelectedLanguage.CultureName.Should().Be("en-GB");
        sut.Text.Culture.Name.Should().Be("en-GB");
        sut.IsDarkSelected.Should().BeTrue();
        MockStore.Received(1).Set(PreferenceService.LanguageKey, "fr-FR");
        MockStore.DidNotReceive().Set(PreferenceService.AppearanceKey, Arg.Any<string>());
        MockAppearance.DidNotReceive().Apply(Arg.Any<AppearanceEnum>());
    }
}
