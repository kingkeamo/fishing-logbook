using System.Collections.ObjectModel;
using System.ComponentModel;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Localization;

namespace FishingLogBook.Mobile.Features.Profile.ViewModels;

public sealed class CataloguePickerViewModel : INotifyPropertyChanged
{
    public const int InitialOptionLimit = 20;

    private readonly IReadOnlyList<CataloguePickerOptionModel> _allOptions;
    private string _search = string.Empty;

    public CataloguePickerViewModel(UiTextService text, string title, IReadOnlyList<CataloguePickerOptionModel> options)
    {
        Text = text;
        Title = title;
        _allOptions = options;
        Options = [];
        foreach (var option in options)
        {
            option.PropertyChanged += OnOptionChanged;
        }

        RefreshOptions();
    }

    public UiTextService Text { get; }
    public string Title { get; }
    public ObservableCollection<CataloguePickerOptionModel> Options { get; }
    public IReadOnlyList<CataloguePickerOptionModel> SelectedOptions => _allOptions.Where(option => option.IsSelected).ToArray();
    public bool HasNoSelection => !_allOptions.Any(option => option.IsSelected);
    public bool HasNoMatches => Options.Count == 0;
    public bool IsLimited => string.IsNullOrWhiteSpace(_search) && _allOptions.Count > InitialOptionLimit;
    public string LimitMessage => string.Format(Text.Culture, Text["PickerShowingLimited"], Options.Count, _allOptions.Count);

    public string Search
    {
        get => _search;
        set
        {
            var search = value ?? string.Empty;
            if (_search == search)
            {
                return;
            }

            _search = search;
            RefreshOptions();
            Publish(nameof(Search));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Toggle(CataloguePickerOptionModel option)
    {
        option.IsSelected = !option.IsSelected;
    }

    public CataloguePickerResultModel ToResult()
    {
        return new CataloguePickerResultModel(_allOptions.Where(option => option.IsSelected).Select(option => option.Id).ToHashSet());
    }

    private void RefreshOptions()
    {
        var term = _search.Trim();
        var visible = term.Length == 0
            ? _allOptions.Take(InitialOptionLimit)
            : _allOptions.Where(option => option.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase));
        Options.Clear();
        foreach (var option in visible)
        {
            Options.Add(option);
        }

        Publish(nameof(HasNoMatches));
        Publish(nameof(IsLimited));
        Publish(nameof(LimitMessage));
    }

    private void OnOptionChanged(object? sender, PropertyChangedEventArgs args)
    {
        Publish(nameof(SelectedOptions));
        Publish(nameof(HasNoSelection));
    }

    private void Publish(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
