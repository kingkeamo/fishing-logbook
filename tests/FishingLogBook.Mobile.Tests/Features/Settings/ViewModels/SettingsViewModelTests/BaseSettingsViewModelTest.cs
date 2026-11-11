using System.Globalization;
using FishingLogBook.Mobile.Features.Settings.Services;
using FishingLogBook.Mobile.Features.Settings.ViewModels;
using FishingLogBook.Mobile.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Settings.ViewModels.SettingsViewModelTests;

public abstract class BaseSettingsViewModelTest
{
    protected readonly IPreferenceStore MockStore = Substitute.For<IPreferenceStore>();
    protected readonly IAppearanceService MockAppearance = Substitute.For<IAppearanceService>();
    protected readonly UiTextService Text = new(CultureInfo.GetCultureInfo("en-GB"));

    protected BaseSettingsViewModelTest()
    {
        MockStore.Get(Arg.Any<string>(), Arg.Any<string>()).Returns(call => call.ArgAt<string>(1));
    }

    protected PreferenceService CreatePreferences(UiTextService? text = null)
    {
        var preferences = new PreferenceService(MockStore, MockAppearance, text ?? Text);
        preferences.Initialize();
        return preferences;
    }

    protected SettingsViewModel CreateSut(PreferenceService? preferences = null)
    {
        return new SettingsViewModel(preferences ?? CreatePreferences(), NullLogger<SettingsViewModel>.Instance,
            NullLogger<LanguageSelectorViewModel>.Instance);
    }
}
