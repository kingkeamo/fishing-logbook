using FishingLogBook.Mobile.Features.Trips.Modals;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;

namespace FishingLogBook.Mobile.Features.Trips.Components.TripCatches;

/// <summary>Record a catch (navigation, raised for the screen) or add existing catches (opens its own modal, raises Changed).</summary>
public partial class TripCatchesView : ContentView
{
    public static readonly BindableProperty TripProperty = BindableProperty.Create(nameof(Trip), typeof(LocalTripRecordModel), typeof(TripCatchesView));
    public static readonly BindableProperty ShowRecordActionProperty = BindableProperty.Create(nameof(ShowRecordAction), typeof(bool), typeof(TripCatchesView), true);
    public static readonly BindableProperty ShowAddActionProperty = BindableProperty.Create(nameof(ShowAddAction), typeof(bool), typeof(TripCatchesView), true);
    public static readonly BindableProperty RecordTextProperty = BindableProperty.Create(nameof(RecordText), typeof(string), typeof(TripCatchesView), string.Empty);
    public static readonly BindableProperty AddTextProperty = BindableProperty.Create(nameof(AddText), typeof(string), typeof(TripCatchesView), string.Empty);

    public TripCatchesView()
    {
        InitializeComponent();
    }

    public event EventHandler? RecordRequested;

    public event EventHandler? Changed;

    public ITripModalService? Modals { get; set; }

    public LocalTripRecordModel? Trip
    {
        get => (LocalTripRecordModel?)GetValue(TripProperty);
        set => SetValue(TripProperty, value);
    }

    public bool ShowRecordAction
    {
        get => (bool)GetValue(ShowRecordActionProperty);
        set => SetValue(ShowRecordActionProperty, value);
    }

    public bool ShowAddAction
    {
        get => (bool)GetValue(ShowAddActionProperty);
        set => SetValue(ShowAddActionProperty, value);
    }

    public string RecordText
    {
        get => (string)GetValue(RecordTextProperty);
        set => SetValue(RecordTextProperty, value);
    }

    public string AddText
    {
        get => (string)GetValue(AddTextProperty);
        set => SetValue(AddTextProperty, value);
    }

    private void OnRecord(object? sender, EventArgs args)
    {
        RecordRequested?.Invoke(this, EventArgs.Empty);
    }

    private async void OnAdd(object? sender, EventArgs args)
    {
        if (Modals is not null && Trip is { } trip && await Modals.AddCatchesAsync(trip))
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
