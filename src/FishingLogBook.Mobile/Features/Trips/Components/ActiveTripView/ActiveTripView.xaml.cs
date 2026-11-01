using FishingLogBook.Mobile.Features.Trips.Modals;
using FishingLogBook.Mobile.Features.Trips.Models;

namespace FishingLogBook.Mobile.Features.Trips.Components.ActiveTripView;

/// <summary>
/// The Web active-trip card: header, facts, counts, timeline and actions, built from an ActiveTripModel only.
/// It opens the participants modal and, through its timeline and catch actions, the note and catch modals itself.
/// Navigation and the trip lifecycle (record a catch, update, finish, photographs) are raised for the page.
/// </summary>
public partial class ActiveTripView : ContentView
{
    public static readonly BindableProperty ModelProperty = BindableProperty.Create(nameof(Model), typeof(ActiveTripModel), typeof(ActiveTripView),
        propertyChanged: (bindable, _, model) =>
        {
            if (((ActiveTripView)bindable).Content is { } content)
            {
                content.BindingContext = model;
            }
        });

    private ITripModalService? _modals;

    public ActiveTripView()
    {
        InitializeComponent();
        Timeline.Changed += OnChanged;
        Timeline.CatchOpenRequested += (_, id) => CatchOpenRequested?.Invoke(this, id);
        CatchActions.Changed += OnChanged;
        CatchActions.RecordRequested += (_, _) => RecordCatchRequested?.Invoke(this, EventArgs.Empty);
        Photos.TakeRequested += (_, _) => TakePhotoRequested?.Invoke(this, EventArgs.Empty);
        Photos.ChooseRequested += (_, _) => ChoosePhotoRequested?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? Changed;

    public event EventHandler<Guid>? CatchOpenRequested;

    public event EventHandler? RecordCatchRequested;

    public event EventHandler? UpdateRequested;

    public event EventHandler? FinishRequested;

    public event EventHandler? LogbookRequested;

    public event EventHandler? PhotoToggleRequested;

    public event EventHandler? TakePhotoRequested;

    public event EventHandler? ChoosePhotoRequested;

    public ITripModalService? Modals
    {
        get => _modals;
        set
        {
            _modals = value;
            Timeline.Modals = value;
            CatchActions.Modals = value;
        }
    }

    public ActiveTripModel? Model
    {
        get => (ActiveTripModel?)GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    private void OnChanged(object? sender, EventArgs args)
    {
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private async void OnParticipants(object? sender, EventArgs args)
    {
        if (_modals is not null && Model is { } model && await _modals.ShowParticipantsAsync(model.Trip.Detail.Trip.Id))
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnToggleAddPhoto(object? sender, EventArgs args)
    {
        PhotoToggleRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnUpdate(object? sender, EventArgs args)
    {
        UpdateRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnFinish(object? sender, EventArgs args)
    {
        FinishRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnLogbook(object? sender, EventArgs args)
    {
        LogbookRequested?.Invoke(this, EventArgs.Empty);
    }
}
