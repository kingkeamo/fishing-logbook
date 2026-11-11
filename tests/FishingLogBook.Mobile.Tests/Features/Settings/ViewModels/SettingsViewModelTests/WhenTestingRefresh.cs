using System.Globalization;
using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Settings.Services;
using FishingLogBook.Mobile.Localization;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Settings.ViewModels.SettingsViewModelTests;

public class WhenTestingRefresh : BaseSettingsViewModelTest
{
    [Fact]
    public void ItShouldRestoreThePersistedSelectionsWhenSettingsIsReopenedWithoutRewritingThem()
    {
        // Arrange
        MockStore.Get(PreferenceService.LanguageKey, "en-GB").Returns("fr-FR");
        MockStore.Get(PreferenceService.AppearanceKey, "System").Returns("Dark");
        using var sut = CreateSut();
        // Act
        sut.Refresh();
        sut.Refresh();
        // Assert
        sut.IsDarkSelected.Should().BeTrue();
        sut.DarkDescription.Should().Be("Sombre, sélectionné");
        sut.SystemDescription.Should().Be("Suivre l’appareil, non sélectionné");
        sut.LightDescription.Should().Be("Clair, non sélectionné");
        sut.Language.SelectedLanguage.Name.Should().Be("Français");
        sut.Language.SelectedLanguage.FlagSource.Should().Be("flag_fr.png");
        MockStore.DidNotReceive().Set(Arg.Any<string>(), Arg.Any<string>());
        MockAppearance.Received(1).Apply(AppearanceEnum.Dark);
    }

    [Theory]
    [InlineData("en-GB", AppearanceEnum.System)]
    [InlineData("en-GB", AppearanceEnum.Light)]
    [InlineData("en-GB", AppearanceEnum.Dark)]
    [InlineData("fr-FR", AppearanceEnum.System)]
    [InlineData("fr-FR", AppearanceEnum.Light)]
    [InlineData("fr-FR", AppearanceEnum.Dark)]
    public void ItShouldReloadBothChoicesAfterChangingThemThroughTheSettingsActions(string culture, AppearanceEnum appearance)
    {
        // Arrange
        var stored = new Dictionary<string, string>();
        MockStore.Get(Arg.Any<string>(), Arg.Any<string>()).Returns(call => stored.GetValueOrDefault(call.ArgAt<string>(0), call.ArgAt<string>(1)));
        MockStore.When(store => store.Set(Arg.Any<string>(), Arg.Any<string>())).Do(call => stored[call.ArgAt<string>(0)] = call.ArgAt<string>(1));
        using var original = CreateSut();
        original.SelectAppearance(appearance == AppearanceEnum.Dark ? AppearanceEnum.Light : AppearanceEnum.Dark);
        original.Language.Select(culture == "fr-FR" ? "en-GB" : "fr-FR");
        MockStore.ClearReceivedCalls();
        MockAppearance.ClearReceivedCalls();
        // Act
        original.SelectAppearance(appearance);
        original.Language.Open();
        original.Language.Select(culture);
        using var reopened = CreateSut(CreatePreferences(new UiTextService(CultureInfo.GetCultureInfo("en-GB"))));
        reopened.Refresh();
        // Assert
        reopened.Language.SelectedLanguage.CultureName.Should().Be(culture);
        reopened.Text["Settings"].Should().Be(culture == "fr-FR" ? "Réglages" : "Settings");
        reopened.IsSystemSelected.Should().Be(appearance == AppearanceEnum.System);
        reopened.IsLightSelected.Should().Be(appearance == AppearanceEnum.Light);
        reopened.IsDarkSelected.Should().Be(appearance == AppearanceEnum.Dark);
        original.Language.IsOpen.Should().BeFalse();
        MockStore.Received(1).Set(PreferenceService.LanguageKey, culture);
        MockStore.Received(1).Set(PreferenceService.AppearanceKey, appearance.ToString());
        MockAppearance.Received(2).Apply(appearance);
    }

    [Fact]
    public void ItShouldUpdateLanguageAndSelectedStateFromExternalPreferenceChanges()
    {
        // Arrange
        var preferences = CreatePreferences();
        using var sut = CreateSut(preferences);
        // Act
        preferences.SetLanguage(1);
        preferences.SetAppearance(AppearanceEnum.Light);
        // Assert
        sut.Language.SelectedLanguage.CultureName.Should().Be("fr-FR");
        sut.LightDescription.Should().Be("Clair, sélectionné");
        sut.Language.AccessibilityDescription.Should().Be("Langue : Français, réduit");
        MockStore.Received(1).Set(PreferenceService.LanguageKey, "fr-FR");
        MockStore.Received(1).Set(PreferenceService.AppearanceKey, "Light");
    }
}
