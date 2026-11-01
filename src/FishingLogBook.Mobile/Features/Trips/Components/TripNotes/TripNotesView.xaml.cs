using FishingLogBook.Mobile.Features.Trips.Modals;
using FishingLogBook.Mobile.Features.Trips.Models;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;

namespace FishingLogBook.Mobile.Features.Trips.Components.TripNotes;

/// <summary>
/// Trip notes: an editable list or a floating add button. It opens the note modal and the remove confirmation
/// itself and raises Changed when a note was saved or removed.
/// </summary>
public partial class TripNotesView : ContentView
{
    public static readonly BindableProperty TripProperty = BindableProperty.Create(nameof(Trip), typeof(LocalTripRecordModel), typeof(TripNotesView));
    public static readonly BindableProperty NotesProperty = BindableProperty.Create(nameof(Notes), typeof(IEnumerable<TripNoteRowModel>), typeof(TripNotesView), propertyChanged: OnChanged);
    public static readonly BindableProperty ShowListProperty = BindableProperty.Create(nameof(ShowList), typeof(bool), typeof(TripNotesView), true, propertyChanged: OnChanged);
    public static readonly BindableProperty UseFloatingTriggerProperty = BindableProperty.Create(nameof(UseFloatingTrigger), typeof(bool), typeof(TripNotesView), false, propertyChanged: OnChanged);
    public static readonly BindableProperty CanAddProperty = BindableProperty.Create(nameof(CanAdd), typeof(bool), typeof(TripNotesView), true, propertyChanged: OnChanged);
    public static readonly BindableProperty HeadingProperty = BindableProperty.Create(nameof(Heading), typeof(string), typeof(TripNotesView), string.Empty);
    public static readonly BindableProperty EmptyTextProperty = BindableProperty.Create(nameof(EmptyText), typeof(string), typeof(TripNotesView), string.Empty);
    public static readonly BindableProperty AddTextProperty = BindableProperty.Create(nameof(AddText), typeof(string), typeof(TripNotesView), string.Empty);
    public static readonly BindableProperty EditLabelProperty = BindableProperty.Create(nameof(EditLabel), typeof(string), typeof(TripNotesView), string.Empty);
    public static readonly BindableProperty DeleteLabelProperty = BindableProperty.Create(nameof(DeleteLabel), typeof(string), typeof(TripNotesView), string.Empty);

    public TripNotesView()
    {
        InitializeComponent();
    }

    public event EventHandler? Changed;

    public ITripModalService? Modals { get; set; }

    public LocalTripRecordModel? Trip
    {
        get => (LocalTripRecordModel?)GetValue(TripProperty);
        set => SetValue(TripProperty, value);
    }

    public IEnumerable<TripNoteRowModel>? Notes
    {
        get => (IEnumerable<TripNoteRowModel>?)GetValue(NotesProperty);
        set => SetValue(NotesProperty, value);
    }

    public bool ShowList
    {
        get => (bool)GetValue(ShowListProperty);
        set => SetValue(ShowListProperty, value);
    }

    public bool UseFloatingTrigger
    {
        get => (bool)GetValue(UseFloatingTriggerProperty);
        set => SetValue(UseFloatingTriggerProperty, value);
    }

    public bool CanAdd
    {
        get => (bool)GetValue(CanAddProperty);
        set => SetValue(CanAddProperty, value);
    }

    public string Heading
    {
        get => (string)GetValue(HeadingProperty);
        set => SetValue(HeadingProperty, value);
    }

    public string EmptyText
    {
        get => (string)GetValue(EmptyTextProperty);
        set => SetValue(EmptyTextProperty, value);
    }

    public string AddText
    {
        get => (string)GetValue(AddTextProperty);
        set => SetValue(AddTextProperty, value);
    }

    public string EditLabel
    {
        get => (string)GetValue(EditLabelProperty);
        set => SetValue(EditLabelProperty, value);
    }

    public string DeleteLabel
    {
        get => (string)GetValue(DeleteLabelProperty);
        set => SetValue(DeleteLabelProperty, value);
    }

    public bool IsEmpty => Notes is null || !Notes.Any();

    public bool ShowFloating => UseFloatingTrigger && CanAdd;

    private static void OnChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        var view = (TripNotesView)bindable;
        view.OnPropertyChanged(nameof(IsEmpty));
        view.OnPropertyChanged(nameof(ShowFloating));
    }

    private async void OnAdd(object? sender, EventArgs args)
    {
        if (Modals is not null && Trip is { } trip && await Modals.EditNoteAsync(trip, null))
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private async void OnEdit(object? sender, EventArgs args)
    {
        if (Modals is not null && Trip is { } trip && (sender as BindableObject)?.BindingContext is TripNoteRowModel row
            && await Modals.EditNoteAsync(trip, row.Id))
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private async void OnDelete(object? sender, EventArgs args)
    {
        if (Modals is not null && Trip is { } trip && (sender as BindableObject)?.BindingContext is TripNoteRowModel row
            && await Modals.RemoveNoteAsync(trip, row.Id))
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
