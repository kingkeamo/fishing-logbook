using FishingLogBook.Mobile.Features.Trips.Modals;
using FishingLogBook.Mobile.Features.Trips.Models;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;

namespace FishingLogBook.Mobile.Features.Trips.Components.TripTimeline;

/// <summary>The chronological rail of a trip. It owns the note edit and remove interactions and raises Changed when one saved.</summary>
public partial class TripTimelineView : ContentView
{
    public static readonly BindableProperty ItemsProperty = BindableProperty.Create(nameof(Items), typeof(IEnumerable<TripTimelineRowModel>), typeof(TripTimelineView));
    public static readonly BindableProperty TripProperty = BindableProperty.Create(nameof(Trip), typeof(LocalTripRecordModel), typeof(TripTimelineView));
    public static readonly BindableProperty EditNoteLabelProperty = BindableProperty.Create(nameof(EditNoteLabel), typeof(string), typeof(TripTimelineView), string.Empty);
    public static readonly BindableProperty DeleteNoteLabelProperty = BindableProperty.Create(nameof(DeleteNoteLabel), typeof(string), typeof(TripTimelineView), string.Empty);

    public TripTimelineView()
    {
        InitializeComponent();
    }

    public event EventHandler? Changed;

    public event EventHandler<Guid>? CatchOpenRequested;

    public ITripModalService? Modals { get; set; }

    public IEnumerable<TripTimelineRowModel>? Items
    {
        get => (IEnumerable<TripTimelineRowModel>?)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public LocalTripRecordModel? Trip
    {
        get => (LocalTripRecordModel?)GetValue(TripProperty);
        set => SetValue(TripProperty, value);
    }

    public string EditNoteLabel
    {
        get => (string)GetValue(EditNoteLabelProperty);
        set => SetValue(EditNoteLabelProperty, value);
    }

    public string DeleteNoteLabel
    {
        get => (string)GetValue(DeleteNoteLabelProperty);
        set => SetValue(DeleteNoteLabelProperty, value);
    }

    private void OnOpenCatch(object? sender, EventArgs args)
    {
        if (RowOf(sender) is { CanOpenCatch: true, CatchId: { } catchId })
        {
            CatchOpenRequested?.Invoke(this, catchId);
        }
    }

    private async void OnEditNote(object? sender, EventArgs args)
    {
        if (Modals is not null && Trip is { } trip && RowOf(sender)?.NoteId is { } noteId
            && trip.Detail.Notes.FirstOrDefault(note => note.Id == noteId) is { } existing
            && await Modals.EditNoteAsync(trip, existing))
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private async void OnDeleteNote(object? sender, EventArgs args)
    {
        if (Modals is not null && Trip is { } trip && RowOf(sender)?.NoteId is { } noteId && await Modals.RemoveNoteAsync(trip, noteId))
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private static TripTimelineRowModel? RowOf(object? sender)
    {
        return (sender as BindableObject)?.BindingContext as TripTimelineRowModel;
    }
}
