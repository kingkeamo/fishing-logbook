using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Settings.Services;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Settings.Services.PreferenceServiceTests;

public class WhenTestingSetAppearance : BasePreferenceServiceTest
{
    [Fact]
    public void ItShouldRejectUnknownThemesWithoutPersistingThem()
    {
        // Arrange
        var sut = CreateSut();
        var action = () => sut.SetAppearance((AppearanceEnum)99);
        // Act
        action.Should().Throw<ArgumentOutOfRangeException>();
        // Assert
        sut.Appearance.Should().Be(AppearanceEnum.System);
        MockStore.DidNotReceive().Set(Arg.Any<string>(), Arg.Any<string>());
        MockAppearance.Received(1).Apply(AppearanceEnum.System);
        MockAppearance.DidNotReceive().Apply((AppearanceEnum)99);
    }

    [Fact]
    public void ItShouldKeepTheCurrentThemeWhenPersistenceFails()
    {
        // Arrange
        var sut = CreateSut();
        MockStore.When(store => store.Set(PreferenceService.AppearanceKey, "Dark")).Do(_ => throw new InvalidOperationException("Unavailable"));
        var action = () => sut.SetAppearance(AppearanceEnum.Dark);
        // Act
        action.Should().Throw<InvalidOperationException>();
        // Assert
        sut.Appearance.Should().Be(AppearanceEnum.System);
        MockStore.Received(1).Set(PreferenceService.AppearanceKey, "Dark");
        MockAppearance.DidNotReceive().Apply(AppearanceEnum.Dark);
    }

    [Fact]
    public void ItShouldNotReapplyAnUnchangedTheme()
    {
        // Arrange
        var sut = CreateSut();
        // Act
        sut.SetAppearance(AppearanceEnum.System);
        // Assert
        sut.Appearance.Should().Be(AppearanceEnum.System);
        MockStore.DidNotReceive().Set(Arg.Any<string>(), Arg.Any<string>());
        MockAppearance.Received(1).Apply(AppearanceEnum.System);
    }

    [Theory]
    [InlineData(AppearanceEnum.Light, "Light")]
    [InlineData(AppearanceEnum.Dark, "Dark")]
    public void ItShouldPersistAndApplyTheSelectedTheme(AppearanceEnum appearance, string stored)
    {
        // Arrange
        var sut = CreateSut();
        // Act
        sut.SetAppearance(appearance);
        // Assert
        sut.Appearance.Should().Be(appearance);
        MockStore.Received(1).Set(PreferenceService.AppearanceKey, stored);
        MockAppearance.Received(1).Apply(appearance);
        MockStore.DidNotReceive().Set(PreferenceService.LanguageKey, Arg.Any<string>());
    }

    [Fact]
    public void ItShouldReturnToTheDeviceTheme()
    {
        // Arrange
        MockStore.Get(PreferenceService.AppearanceKey, "System").Returns("Dark");
        var sut = CreateSut();
        // Act
        sut.SetAppearance(AppearanceEnum.System);
        // Assert
        sut.Appearance.Should().Be(AppearanceEnum.System);
        MockStore.Received(1).Set(PreferenceService.AppearanceKey, "System");
        MockAppearance.Received(1).Apply(AppearanceEnum.System);
    }
}
