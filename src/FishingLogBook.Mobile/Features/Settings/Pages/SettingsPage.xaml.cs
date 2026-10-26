using FishingLogBook.Mobile.Features.Settings.Services;

namespace FishingLogBook.Mobile.Features.Settings.Pages;

public partial class SettingsPage : ContentPage
{
    private readonly PreferenceService _preferences;
    private bool _refreshing;

    public SettingsPage(PreferenceService preferences)
    {
        _preferences = preferences;
        InitializeComponent();
        BindingContext = preferences;
        RefreshPickers();
    }

    private void RefreshPickers()
    {
        _refreshing = true;
        LanguagePicker.ItemsSource = _preferences.Languages.ToList();
        ThemePicker.ItemsSource = _preferences.Themes.ToList();
        LanguagePicker.SelectedIndex = _preferences.LanguageIndex;
        ThemePicker.SelectedIndex = (int)_preferences.Appearance;
        _refreshing = false;
    }

    private void OnLanguageChanged(object? sender, EventArgs args)
    {
        if (!_refreshing && LanguagePicker.SelectedIndex >= 0)
        {
            _preferences.SetLanguage(LanguagePicker.SelectedIndex);
            RefreshPickers();
        }
    }

    private void OnAppearanceChanged(object? sender, EventArgs args)
    {
        if (!_refreshing && ThemePicker.SelectedIndex >= 0)
        {
            _preferences.SetAppearance((AppearanceEnum)ThemePicker.SelectedIndex);
        }
    }
}
