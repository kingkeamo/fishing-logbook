using System.Globalization;
using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Settings.Services;
using FishingLogBook.Mobile.Localization;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Settings.Services.PreferenceServiceTests;

public class WhenTestingInitialize : BasePreferenceServiceTest
{
    [Theory]
    [InlineData(0, AppearanceEnum.System)]
    [InlineData(0, AppearanceEnum.Light)]
    [InlineData(0, AppearanceEnum.Dark)]
    [InlineData(1, AppearanceEnum.System)]
    [InlineData(1, AppearanceEnum.Light)]
    [InlineData(1, AppearanceEnum.Dark)]
    public void ItShouldReloadBothSelectionsAfterChangingThemWithoutRewritingStoredChoices(int languageIndex, AppearanceEnum appearance)
    {
        // Arrange
        var stored = new Dictionary<string, string>();
        MockStore.Get(Arg.Any<string>(), Arg.Any<string>()).Returns(call =>
            stored.GetValueOrDefault(call.ArgAt<string>(0), call.ArgAt<string>(1)));
        MockStore.When(store => store.Set(Arg.Any<string>(), Arg.Any<string>())).Do(call =>
            stored[call.ArgAt<string>(0)] = call.ArgAt<string>(1));
        var sut = CreateSut();
        sut.SetLanguage(1 - languageIndex);
        sut.SetAppearance(appearance == AppearanceEnum.Dark ? AppearanceEnum.Light : AppearanceEnum.Dark);
        MockStore.ClearReceivedCalls();
        MockAppearance.ClearReceivedCalls();
        var culture = languageIndex == 1 ? "fr-FR" : "en-GB";

        // Act
        sut.SetLanguage(languageIndex);
        sut.SetAppearance(appearance);
        var reloaded = new PreferenceService(MockStore, MockAppearance, new UiTextService(CultureInfo.GetCultureInfo("en-GB")));
        reloaded.Initialize();
        reloaded.Initialize();
        reloaded.SetLanguage(languageIndex);
        reloaded.SetAppearance(appearance);

        // Assert
        reloaded.LanguageIndex.Should().Be(languageIndex);
        reloaded.Text.Culture.Name.Should().Be(culture);
        reloaded.Appearance.Should().Be(appearance);
        reloaded.Themes.Should().Equal(sut.Themes);
        MockStore.Received(1).Set(PreferenceService.LanguageKey, culture);
        MockStore.Received(1).Set(PreferenceService.AppearanceKey, appearance.ToString());
        MockStore.Received(1).Get(PreferenceService.LanguageKey, "en-GB");
        MockStore.Received(1).Get(PreferenceService.AppearanceKey, "System");
        MockAppearance.Received(2).Apply(appearance);
    }

    [Theory]
    [InlineData("invalid", "99", "en-GB", AppearanceEnum.System)]
    [InlineData("fr-FR", "Dark", "fr-FR", AppearanceEnum.Dark)]
    [InlineData("en-GB", "Light", "en-GB", AppearanceEnum.Light)]
    public void ItShouldRestoreSupportedChoicesWithoutWritingOnStartup(string language, string appearance, string expectedLanguage, AppearanceEnum expectedAppearance)
    {
        // Arrange
        MockStore.Get(PreferenceService.LanguageKey, "en-GB").Returns(language);
        MockStore.Get(PreferenceService.AppearanceKey, "System").Returns(appearance);
        var sut = CreateUninitializedSut();
        // Act
        sut.Initialize();
        // Assert
        sut.Text.Culture.Name.Should().Be(expectedLanguage);
        sut.Appearance.Should().Be(expectedAppearance);
        MockStore.Received(1).Get(PreferenceService.LanguageKey, "en-GB");
        MockStore.Received(1).Get(PreferenceService.AppearanceKey, "System");
        MockAppearance.Received(1).Apply(expectedAppearance);
        MockStore.DidNotReceive().Set(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public void ItShouldInitializeOnlyOnce()
    {
        // Arrange
        var sut = CreateUninitializedSut();
        // Act
        sut.Initialize();
        sut.Initialize();
        // Assert
        MockStore.Received(1).Get(PreferenceService.LanguageKey, "en-GB");
        MockStore.Received(1).Get(PreferenceService.AppearanceKey, "System");
        MockAppearance.Received(1).Apply(AppearanceEnum.System);
    }
}
