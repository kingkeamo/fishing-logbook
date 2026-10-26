using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Settings.Services;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Settings.Services.PreferenceServiceTests;

public class WhenTestingConstructor : BasePreferenceServiceTest
{
    [Theory]
    [InlineData("invalid", "99", "en-GB", AppearanceEnum.System)]
    [InlineData("fr-FR", "Dark", "fr-FR", AppearanceEnum.Dark)]
    [InlineData("en-GB", "Light", "en-GB", AppearanceEnum.Light)]
    public void ItShouldRestoreSupportedChoicesWithoutWritingOnStartup(string language, string appearance, string expectedLanguage, AppearanceEnum expectedAppearance)
    {
        // Arrange
        MockStore.Get(PreferenceService.LanguageKey, "en-GB").Returns(language);
        MockStore.Get(PreferenceService.AppearanceKey, "System").Returns(appearance);
        // Act
        var sut = CreateSut();
        // Assert
        sut.Text.Culture.Name.Should().Be(expectedLanguage);
        sut.Appearance.Should().Be(expectedAppearance);
        MockStore.Received(1).Get(PreferenceService.LanguageKey, "en-GB");
        MockStore.Received(1).Get(PreferenceService.AppearanceKey, "System");
        MockAppearance.Received(1).Apply(expectedAppearance);
        MockStore.DidNotReceive().Set(Arg.Any<string>(), Arg.Any<string>());
    }
}
