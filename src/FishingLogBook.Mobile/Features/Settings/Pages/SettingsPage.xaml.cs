using FishingLogBook.Mobile.Features.Settings.Services;

namespace FishingLogBook.Mobile.Features.Settings.Pages;

public partial class SettingsPage : ContentPage
{
    private readonly PreferenceService _preferences;
    private readonly LocalDataService _localData;
    private bool _refreshing;

    public SettingsPage(PreferenceService preferences, LocalDataService localData)
    {
        _preferences = preferences;
        _localData = localData;
        InitializeComponent();
        BindingContext = preferences;
        RefreshSelection();
    }

    private void RefreshSelection()
    {
        _refreshing = true;
        LanguageEnglish.IsChecked = _preferences.LanguageIndex == 0;
        LanguageFrench.IsChecked = _preferences.LanguageIndex == 1;
        ThemeSystem.IsChecked = _preferences.Appearance == AppearanceEnum.System;
        ThemeLight.IsChecked = _preferences.Appearance == AppearanceEnum.Light;
        ThemeDark.IsChecked = _preferences.Appearance == AppearanceEnum.Dark;
        _refreshing = false;
    }

    private void OnLanguageChanged(object? sender, CheckedChangedEventArgs args)
    {
        if (_refreshing || !args.Value)
        {
            return;
        }

        _preferences.SetLanguage(ReferenceEquals(sender, LanguageFrench) ? 1 : 0);
        RefreshSelection();
    }

    private void OnAppearanceChanged(object? sender, CheckedChangedEventArgs args)
    {
        if (_refreshing || !args.Value)
        {
            return;
        }

        var appearance = ReferenceEquals(sender, ThemeLight)
            ? AppearanceEnum.Light
            : ReferenceEquals(sender, ThemeDark) ? AppearanceEnum.Dark : AppearanceEnum.System;
        _preferences.SetAppearance(appearance);
    }

    private async void OnClearLocalData(object? sender, EventArgs args)
    {
        await _localData.ClearAsync();
    }
}
