using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Settings.Services;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Settings.ViewModels.SettingsViewModelTests;

public class WhenTestingDispose : BaseSettingsViewModelTest
{
    [Fact]
    public void ItShouldStopListeningToPreferenceChangesAfterDisposal()
    {
        // Arrange
        var preferences = CreatePreferences();
        var sut = CreateSut(preferences);
        var changes = new List<string?>();
        sut.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
        // Act
        sut.Dispose();
        preferences.SetAppearance(AppearanceEnum.Dark);
        // Assert
        changes.Should().BeEmpty();
        MockStore.Received(1).Set(PreferenceService.AppearanceKey, "Dark");
    }
}
