using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Settings.Services;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Settings.ViewModels.SettingsViewModelTests;

public class WhenTestingSelectAppearance : BaseSettingsViewModelTest
{
    [Fact]
    public void ItShouldRetainTheSelectedThemeAndReportFailedPersistence()
    {
        // Arrange
        using var sut = CreateSut();
        MockStore.When(store => store.Set(PreferenceService.AppearanceKey, "Dark")).Do(_ => throw new IOException("Unavailable"));
        // Act
        var selected = sut.SelectAppearance(AppearanceEnum.Dark);
        // Assert
        selected.Should().BeFalse();
        sut.HasStatus.Should().BeTrue();
        sut.StatusText.Should().Be(Text["SettingsSaveFailed"]);
        sut.IsSystemSelected.Should().BeTrue();
        sut.IsDarkSelected.Should().BeFalse();
        MockStore.Received(1).Set(PreferenceService.AppearanceKey, "Dark");
        MockAppearance.DidNotReceive().Apply(AppearanceEnum.Dark);
    }

    [Fact]
    public void ItShouldNotRewriteOrReapplyTheCurrentTheme()
    {
        // Arrange
        using var sut = CreateSut();
        MockAppearance.ClearReceivedCalls();
        // Act
        sut.SelectAppearance(AppearanceEnum.System).Should().BeTrue();
        // Assert
        sut.IsSystemSelected.Should().BeTrue();
        MockStore.DidNotReceive().Set(Arg.Any<string>(), Arg.Any<string>());
        MockAppearance.DidNotReceive().Apply(Arg.Any<AppearanceEnum>());
    }

    [Theory]
    [InlineData(AppearanceEnum.System)]
    [InlineData(AppearanceEnum.Light)]
    [InlineData(AppearanceEnum.Dark)]
    public void ItShouldApplyPersistAndSelectOnlyTheRequestedTheme(AppearanceEnum selected)
    {
        // Arrange
        var preferences = CreatePreferences();
        preferences.SetAppearance(selected == AppearanceEnum.Dark ? AppearanceEnum.Light : AppearanceEnum.Dark);
        using var sut = CreateSut(preferences);
        MockStore.ClearReceivedCalls();
        MockAppearance.ClearReceivedCalls();
        var changes = new List<string?>();
        sut.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
        // Act
        var result = sut.SelectAppearance(selected);
        // Assert
        result.Should().BeTrue();
        sut.IsSystemSelected.Should().Be(selected == AppearanceEnum.System);
        sut.IsLightSelected.Should().Be(selected == AppearanceEnum.Light);
        sut.IsDarkSelected.Should().Be(selected == AppearanceEnum.Dark);
        changes.Should().Contain(nameof(sut.IsSystemSelected)).And.Contain(nameof(sut.IsLightSelected)).And.Contain(nameof(sut.IsDarkSelected));
        sut.HasStatus.Should().BeFalse();
        MockStore.Received(1).Set(PreferenceService.AppearanceKey, selected.ToString());
        MockStore.DidNotReceive().Set(PreferenceService.LanguageKey, Arg.Any<string>());
        MockAppearance.Received(1).Apply(selected);
    }
}
