using System.ComponentModel;
using FishingLogBook.Mobile.Features.Settings.Models;
using FishingLogBook.Mobile.Features.Settings.Services;
using FishingLogBook.Mobile.Localization;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Mobile.Features.Settings.ViewModels;

public sealed class SettingsViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly PreferenceService _preferences;
    private readonly LanguageOptionModel[] _languages;
    private readonly ILogger<SettingsViewModel> _logger;

    public SettingsViewModel(PreferenceService preferences, ILogger<SettingsViewModel> logger, ILogger<LanguageSelectorViewModel> languageLogger)
    {
        _preferences = preferences;
        _logger = logger;
        _languages =
        [
            new("en-GB", Text["LanguageEnglish"], "flag_gb.png"),
            new("fr-FR", Text["LanguageFrench"], "flag_fr.png")
        ];
        Language = new LanguageSelectorViewModel(Text, _languages, Text.Culture.Name, SelectLanguage, languageLogger);
        preferences.PropertyChanged += OnPreferenceChanged;
    }

    public UiTextService Text => _preferences.Text;
    public LanguageSelectorViewModel Language { get; }
    public string? StatusText { get; private set; }
    public bool HasStatus => StatusText is not null;
    public bool IsSystemSelected => _preferences.Appearance == AppearanceEnum.System;
    public bool IsLightSelected => _preferences.Appearance == AppearanceEnum.Light;
    public bool IsDarkSelected => _preferences.Appearance == AppearanceEnum.Dark;
    public string SystemDescription => DescribeTheme("ThemeSystem", IsSystemSelected);
    public string LightDescription => DescribeTheme("ThemeLight", IsLightSelected);
    public string DarkDescription => DescribeTheme("ThemeDark", IsDarkSelected);
    public event PropertyChangedEventHandler? PropertyChanged;

    public bool SelectAppearance(AppearanceEnum appearance)
    {
        try
        {
            _preferences.SetAppearance(appearance);
            SetStatus(null);
            return true;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "The selected appearance could not be saved.");
            SetStatus(Text["SettingsSaveFailed"]);
            return false;
        }
    }

    public void Refresh()
    {
        Language.Restore(Text.Culture.Name);
        foreach (var property in new[] { nameof(IsSystemSelected), nameof(IsLightSelected), nameof(IsDarkSelected),
            nameof(SystemDescription), nameof(LightDescription), nameof(DarkDescription) })
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }
    }

    public void Dispose()
    {
        _preferences.PropertyChanged -= OnPreferenceChanged;
    }

    private void SelectLanguage(string cultureName)
    {
        _preferences.SetLanguage(Array.FindIndex(_languages, language => language.CultureName == cultureName));
    }

    private string DescribeTheme(string key, bool selected)
    {
        return string.Format(Text.Culture,
            Text[selected ? "SelectionSelectedDescription" : "SelectionNotSelectedDescription"], Text[key]);
    }

    private void OnPreferenceChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(PreferenceService.Appearance) or nameof(PreferenceService.LanguageIndex))
        {
            Refresh();
        }
    }

    private void SetStatus(string? value)
    {
        StatusText = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusText)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasStatus)));
    }
}
