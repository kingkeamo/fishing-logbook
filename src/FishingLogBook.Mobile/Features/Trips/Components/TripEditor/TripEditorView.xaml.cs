using FishingLogBook.Mobile.Features.Trips.Modals;
using FishingLogBook.Mobile.Features.Trips.Models;
using FishingLogBook.Mobile.Features.Trips.ViewModels;

namespace FishingLogBook.Mobile.Features.Trips.Components.TripEditor;

/// <summary>
/// The Web trip editor. Its BindingContext is the editor's own view-model. It owns the title, location,
/// photograph, participant and catch-removal interactions; anything that leaves the screen (recording a catch,
/// saving, cancelling) or needs a refresh is raised for the page.
/// </summary>
public partial class TripEditorView : ContentView
{
    private ITripModalService? _modals;

    public TripEditorView()
    {
        InitializeComponent();
        Notes.Changed += OnChanged;
        CatchActions.Changed += OnChanged;
        CatchActions.RecordRequested += (_, _) => RecordCatchRequested?.Invoke(this, EventArgs.Empty);
        Photos.TakeRequested += async (_, _) => await AddPhotographAsync(useCamera: true);
        Photos.ChooseRequested += async (_, _) => await AddPhotographAsync(useCamera: false);
        Photos.RemoveRequested += async (_, id) => await ViewModel!.RemovePhotographAsync(id, CancellationToken.None);
    }

    public event EventHandler? Changed;

    public event EventHandler? RecordCatchRequested;

    public event EventHandler? SaveRequested;

    public event EventHandler? CancelRequested;

    public ITripModalService? Modals
    {
        get => _modals;
        set
        {
            _modals = value;
            Notes.Modals = value;
            CatchActions.Modals = value;
        }
    }

    private TripEditViewModel? ViewModel => BindingContext as TripEditViewModel;

    private async Task AddPhotographAsync(bool useCamera)
    {
        if (ViewModel is { } viewModel)
        {
            await viewModel.AddPhotographAsync(useCamera, CancellationToken.None);
        }
    }

    private void OnChanged(object? sender, EventArgs args)
    {
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private async void OnParticipants(object? sender, EventArgs args)
    {
        if (_modals is not null && ViewModel?.TripId is { } tripId && await _modals.ShowParticipantsAsync(tripId))
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private async void OnRemoveCatch(object? sender, EventArgs args)
    {
        if (_modals is not null && (sender as BindableObject)?.BindingContext is TripEditCatchModel item
            && await _modals.RemoveCatchAsync(item.Id, item.Label))
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnSave(object? sender, EventArgs args)
    {
        SaveRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnCancel(object? sender, EventArgs args)
    {
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }
}
