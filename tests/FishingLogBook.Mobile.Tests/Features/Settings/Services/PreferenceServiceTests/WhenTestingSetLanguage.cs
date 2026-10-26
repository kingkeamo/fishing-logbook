using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Settings.Services;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Settings.Services.PreferenceServiceTests;

public class WhenTestingSetLanguage : BasePreferenceServiceTest
{
    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void ItShouldRejectUnsupportedSelections(int index)
    {
        // Arrange
        var sut = CreateSut();
        var action = () => sut.SetLanguage(index);
        // Act
        action.Should().Throw<ArgumentOutOfRangeException>();
        // Assert
        sut.LanguageIndex.Should().Be(0);
        MockStore.DidNotReceive().Set(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public void ItShouldKeepTheCurrentLanguageIfSavingFails()
    {
        // Arrange
        var sut = CreateSut();
        MockStore.When(store => store.Set(PreferenceService.LanguageKey, "fr-FR")).Do(_ => throw new InvalidOperationException("Storage unavailable"));
        var action = () => sut.SetLanguage(1);
        // Act
        action.Should().Throw<InvalidOperationException>();
        // Assert
        sut.LanguageIndex.Should().Be(0);
        sut.Text.Culture.Name.Should().Be("en-GB");
        MockStore.Received(1).Set(PreferenceService.LanguageKey, "fr-FR");
    }

    [Fact]
    public void ItShouldNotRewriteAnUnchangedPreference()
    {
        // Arrange
        var sut = CreateSut();
        // Act
        sut.SetLanguage(0);
        // Assert
        sut.LanguageIndex.Should().Be(0);
        MockStore.DidNotReceive().Set(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public void ItShouldPersistLanguageAndRefreshLabelsWithoutChangingAppearance()
    {
        // Arrange
        MockStore.Get(PreferenceService.AppearanceKey, "System").Returns("Dark");
        var sut = CreateSut();
        var changes = new List<string?>();
        sut.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
        // Act
        sut.SetLanguage(1);
        // Assert
        sut.LanguageIndex.Should().Be(1);
        sut.Text.Culture.Name.Should().Be("fr-FR");
        sut.Appearance.Should().Be(AppearanceEnum.Dark);
        sut.Themes.Should().Equal("Suivre l’appareil", "Clair", "Sombre");
        changes.Should().Contain(nameof(sut.LanguageIndex)).And.Contain(nameof(sut.Themes));
        MockStore.Received(1).Set(PreferenceService.LanguageKey, "fr-FR");
        MockStore.DidNotReceive().Set(PreferenceService.AppearanceKey, Arg.Any<string>());
        MockAppearance.Received(1).Apply(AppearanceEnum.Dark);
    }
}
