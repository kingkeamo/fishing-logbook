using System.Collections.ObjectModel;
using System.ComponentModel;
using FishingLogBook.Mobile.Features.Import.Models;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Shared.Constants;

namespace FishingLogBook.Mobile.Features.Import.ViewModels;

public sealed class PlaceNameModalViewModel : INotifyPropertyChanged
{
    public PlaceNameModalViewModel(UiTextService text, PlaceNameModalModel model)
    {
        Text = text;
        var components = model.Components
            .Where(component => !string.IsNullOrWhiteSpace(component))
            .Select(component => component.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var current = model.CurrentPlaceName?.Split(
            ',',
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var selected = current is null
            ? components.ToHashSet(StringComparer.OrdinalIgnoreCase)
            : current.Where(component => components.Contains(component, StringComparer.OrdinalIgnoreCase))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var component in components)
        {
            var item = new PlaceNameComponentModel(component, selected.Contains(component));
            item.PropertyChanged += OnComponentChanged;
            Components.Add(item);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public UiTextService Text { get; }

    public ObservableCollection<PlaceNameComponentModel> Components { get; } = [];

    public string? Preview
    {
        get
        {
            var value = string.Join(", ", Components.Where(component => component.IsSelected).Select(component => component.Text));
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }

    public string PreviewText => Preview ?? Text["Import_NoPlaceName"];

    public bool IsTooLong => Preview is { Length: > CatchDetailConstants.MaxPlaceNameLength };

    public bool CanApply => !IsTooLong;

    public PlaceNameModalResult ToResult()
    {
        return new PlaceNameModalResult(Preview);
    }

    public void Toggle(PlaceNameComponentModel component)
    {
        component.IsSelected = !component.IsSelected;
    }

    private void OnComponentChanged(object? sender, PropertyChangedEventArgs args)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Preview)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PreviewText)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsTooLong)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanApply)));
    }
}
