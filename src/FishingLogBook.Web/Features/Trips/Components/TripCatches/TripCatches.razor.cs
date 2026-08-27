using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Trips.Components.TripCatches;

public partial class TripCatches : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private IReadOnlyList<CatchModel> _unassigned = [];
    private bool _isOpen;
    private bool _isSaving;
    private bool _saveFailed;

    [Parameter]
    [EditorRequired]
    public TripModel Trip { get; set; } = default!;

    [Parameter]
    public string RecordCatchBaseHref { get; set; } = "/catches/record";

    [Parameter]
    public EventCallback OnCatchesAttached { get; set; }

    [Inject]
    private ICatchStore CatchStore { get; set; } = default!;

    [Inject]
    private ILoggingService Logging { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private string RecordCatchHref => $"{RecordCatchBaseHref}?tripId={Trip.Id:D}";

    private async Task OpenAsync()
    {
        _isOpen = true;
        _saveFailed = false;
        await LoadUnassignedAsync();
    }

    private void Close()
    {
        _isOpen = false;
        _unassigned = [];
        _saveFailed = false;
    }

    private async Task LoadUnassignedAsync()
    {
        try
        {
            var catches = await CatchStore.GetMetadataAsync(
                Trip.OwnerUserId,
                _cancellationTokenSource.Token);
            _unassigned =
            [
                .. catches
                    .Where(catchRecord => catchRecord.TripId is null)
                    .OrderByDescending(catchRecord => catchRecord.CaughtOn)
            ];
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _unassigned = [];
            _saveFailed = true;
            await Logging.LogErrorAsync(
                "reading catches that are not on a trip",
                exception,
                CancellationToken.None);
        }
    }

    private async Task AttachAsync(IReadOnlyList<Guid> catchIds)
    {
        if (catchIds.Count == 0 || _isSaving)
        {
            return;
        }

        _isSaving = true;
        _saveFailed = false;
        try
        {
            foreach (var catchId in catchIds)
            {
                await CatchStore.UpdateTripAsync(
                    Trip.OwnerUserId,
                    catchId,
                    Trip.Id,
                    _cancellationTokenSource.Token);
            }

            Close();
            if (OnCatchesAttached.HasDelegate)
            {
                await OnCatchesAttached.InvokeAsync();
            }
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _saveFailed = true;
            await Logging.LogErrorAsync("adding catches to a trip", exception, CancellationToken.None);
        }
        finally
        {
            _isSaving = false;
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
