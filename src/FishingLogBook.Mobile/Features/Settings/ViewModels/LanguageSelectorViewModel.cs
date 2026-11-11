using System.ComponentModel;
using FishingLogBook.Mobile.Features.Settings.Models;
using FishingLogBook.Mobile.Localization;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Mobile.Features.Settings.ViewModels;

public sealed class LanguageSelectorViewModel : INotifyPropertyChanged
{
    private readonly IReadOnlyList<LanguageOptionModel> _languages;
    private readonly Action<string> _select;
    private readonly ILogger<LanguageSelectorViewModel> _logger;
    private string _selectedCulture;

    public LanguageSelectorViewModel(
        UiTextService text,
        IReadOnlyList<LanguageOptionModel> languages,
        string selectedCulture,
        Action<string> select,
        ILogger<LanguageSelectorViewModel> logger)
    {
        if (languages.Count == 0 || languages.Select(language => language.CultureName).Distinct().Count() != languages.Count)
        {
            throw new ArgumentException("Language options must be non-empty and unique.", nameof(languages));
        }

        Text = text;
        _languages = languages.ToArray();
        _select = select;
        _logger = logger;
        _selectedCulture = RequireLanguage(selectedCulture).CultureName;
    }

    public UiTextService Text { get; }
    public bool IsOpen { get; private set; }
    public string? StatusText { get; private set; }
    public bool HasStatus => StatusText is not null;
    public LanguageOptionModel SelectedLanguage => Describe(RequireLanguage(_selectedCulture));
    public IReadOnlyList<LanguageOptionModel> Options => _languages.Select(Describe).ToArray();
    public string AccessibilityDescription => string.Format(
        Text.Culture, Text["LanguageSelectorDescription"], SelectedLanguage.Name,
        Text[IsOpen ? "SelectionExpanded" : "SelectionCollapsed"]);
    public event PropertyChangedEventHandler? PropertyChanged;

    public bool Open()
    {
        if (IsOpen)
        {
            return false;
        }

        SetOpen(true);
        return true;
    }

    public void Close()
    {
        if (IsOpen)
        {
            SetOpen(false);
        }
    }

    public bool Select(string cultureName)
    {
        var language = RequireLanguage(cultureName);
        if (_selectedCulture != language.CultureName)
        {
            try
            {
                _select(language.CultureName);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "The selected language could not be saved.");
                SetStatus(Text["SettingsSaveFailed"]);
                return false;
            }
            if (_selectedCulture != language.CultureName)
            {
                Restore(language.CultureName);
            }
        }

        Close();
        SetStatus(null);
        return true;
    }

    public void Restore(string cultureName)
    {
        _selectedCulture = RequireLanguage(cultureName).CultureName;
        Refresh();
    }

    public void Refresh()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedLanguage)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Options)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AccessibilityDescription)));
    }

    private LanguageOptionModel RequireLanguage(string cultureName)
    {
        return _languages.SingleOrDefault(language => language.CultureName == cultureName)
            ?? throw new ArgumentOutOfRangeException(nameof(cultureName));
    }

    private LanguageOptionModel Describe(LanguageOptionModel language)
    {
        var selected = language.CultureName == _selectedCulture;
        return language with
        {
            IsSelected = selected,
            AccessibilityDescription = string.Format(Text.Culture,
                Text[selected ? "SelectionSelectedDescription" : "SelectionNotSelectedDescription"], language.Name)
        };
    }

    private void SetOpen(bool value)
    {
        SetStatus(null);
        IsOpen = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsOpen)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AccessibilityDescription)));
    }

    private void SetStatus(string? value)
    {
        StatusText = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusText)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasStatus)));
    }
}
