using FishingLogBook.Mobile.Features.Catch.Models;
using FishingLogBook.Mobile.Native.Controls;
using FishingLogBook.Shared.Constants;

namespace FishingLogBook.Mobile.Features.Trips.Components.TripLocationPicker;

/// <summary>Saved-location chips, an other-location entry and clear. It only edits PlaceName, which the owner binds two-way.</summary>
public partial class TripLocationPickerView : ContentView
{
    public static readonly BindableProperty OptionsProperty = BindableProperty.Create(nameof(Options), typeof(IEnumerable<CatchChipOptionModel>), typeof(TripLocationPickerView), propertyChanged: OnChanged);
    public static readonly BindableProperty PlaceNameProperty = BindableProperty.Create(nameof(PlaceName), typeof(string), typeof(TripLocationPickerView), string.Empty,
        BindingMode.TwoWay, propertyChanged: OnChanged);
    public static readonly BindableProperty HeadingProperty = BindableProperty.Create(nameof(Heading), typeof(string), typeof(TripLocationPickerView), string.Empty);
    public static readonly BindableProperty OtherTextProperty = BindableProperty.Create(nameof(OtherText), typeof(string), typeof(TripLocationPickerView), string.Empty);
    public static readonly BindableProperty ClearTextProperty = BindableProperty.Create(nameof(ClearText), typeof(string), typeof(TripLocationPickerView), string.Empty);

    public TripLocationPickerView()
    {
        InitializeComponent();
    }

    public IEnumerable<CatchChipOptionModel>? Options
    {
        get => (IEnumerable<CatchChipOptionModel>?)GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

    public string PlaceName
    {
        get => (string)GetValue(PlaceNameProperty);
        set => SetValue(PlaceNameProperty, value);
    }

    public string Heading
    {
        get => (string)GetValue(HeadingProperty);
        set => SetValue(HeadingProperty, value);
    }

    public string OtherText
    {
        get => (string)GetValue(OtherTextProperty);
        set => SetValue(OtherTextProperty, value);
    }

    public string ClearText
    {
        get => (string)GetValue(ClearTextProperty);
        set => SetValue(ClearTextProperty, value);
    }

    public bool HasOptions => Options is not null && Options.Any();

    public bool HasPlace => !string.IsNullOrWhiteSpace(PlaceName);

    private static void OnChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        var view = (TripLocationPickerView)bindable;
        view.OnPropertyChanged(nameof(HasOptions));
        view.OnPropertyChanged(nameof(HasPlace));
    }

    private void OnChip(object? sender, EventArgs args)
    {
        if (sender is ChipView { CommandParameter: string name })
        {
            PlaceName = TripConstants.TrimPlaceName(name) ?? string.Empty;
        }
    }

    private void OnClear(object? sender, EventArgs args)
    {
        PlaceName = string.Empty;
    }
}
