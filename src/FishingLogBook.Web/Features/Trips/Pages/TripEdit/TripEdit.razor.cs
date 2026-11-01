using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Profile.Providers;
using FishingLogBook.Web.Features.Trips.Clients;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
using FishingLogBook.Web.Features.Trips.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Trips.Pages.TripEdit;

public partial class TripEdit : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private TripModel? _trip;
    private TripDisplayModel? _display;
    private IReadOnlyList<CatchModel> _catches = [];
    private WeightUnitEnum _weightUnit = WeightUnitEnum.Kg;
    private LengthUnitEnum _lengthUnit = LengthUnitEnum.Cm;
    private bool _isLoading = true;
    private bool _loadFailed;
    private Guid _viewerUserId;

    [Inject]
    private ITripClient TripClient { get; set; } = default!;

    [Parameter]
    public Guid TripId { get; set; }

    [Inject]
    private ITripStore TripStore { get; set; } = default!;

    [Inject]
    private ICatchStore CatchStore { get; set; } = default!;

    [Inject]
    private ILocalCatchOwnerService LocalOwner { get; set; } = default!;

    [Inject]
    private ITripDisplayService TripDisplay { get; set; } = default!;

    [Inject]
    private IAnglerPreferencesProvider AnglerPreferences { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private ILoggingService Logging { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private string? SummaryLabel
    {
        get
        {
            if (_display?.StartedDate is null)
            {
                return null;
            }

            return _display.StartedTime is null
                ? _display.StartedDate
                : $"{_display.StartedDate} · {_display.StartedTime}";
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _isLoading = true;
        _loadFailed = false;
        var cancellationToken = _cancellationTokenSource.Token;
        try
        {
            var ownerUserId = await LocalOwner.GetUserIdAsync(cancellationToken);
            _viewerUserId = ownerUserId;
            _trip = await TripStore.GetAsync(ownerUserId, TripId, cancellationToken);
            if (_trip is null)
            {
                _trip = await LoadRemoteOwnedTripAsync(ownerUserId, cancellationToken);
            }
            if (_trip is { } plannedOrCancelled && (plannedOrCancelled.IsPlanned || plannedOrCancelled.IsCancelled))
            {
                Navigation.NavigateTo($"/trips/{TripId:D}/plan", replace: true);
                return;
            }

            if (_trip is null)
            {
                return;
            }

            await RefreshPhotographUrlsAsync(cancellationToken);

            _display = await TripDisplay.DescribeAsync(_trip, cancellationToken);
            _catches = await LoadCatchesAsync(ownerUserId, cancellationToken);
            await ReadPreferencesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            _loadFailed = true;
            _trip = null;
            await Logging.LogErrorAsync("loading a trip for editing", exception, CancellationToken.None);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task RefreshPhotographUrlsAsync(CancellationToken cancellationToken)
    {
        if (_trip is null || _trip.Photographs.Count == 0 || _trip.Photographs.Any(photo => photo.RemoteUrl is not null))
        {
            return;
        }
        try
        {
            var detail = await TripClient.GetDetailAsync(TripId, cancellationToken);
            if (detail is not null)
            {
                var photographs = detail.Photographs.ToDictionary(photo => photo.Id);
                _trip = _trip with
                {
                    Photographs = [.. _trip.Photographs.Select(photo => photo with
                {
                    RemoteUrl = photographs.GetValueOrDefault(photo.Id)?.Url
                })]
                };
            }
        }
        catch (HttpRequestException exception)
        {
            await Logging.LogErrorAsync("refreshing trip photograph previews", exception, CancellationToken.None);
        }
    }

    private async Task<TripModel?> LoadRemoteOwnedTripAsync(Guid viewerUserId, CancellationToken cancellationToken)
    {
        var detail = await TripClient.GetDetailAsync(TripId, cancellationToken);
        if (detail is null || detail.Trip.Id != TripId || detail.Trip.OwnerUserId != viewerUserId || detail.Role != TripParticipantConstants.Owner)
        {
            return null;
        }
        var trip = detail.Trip;
        var local = new TripModel(trip.Id, trip.OwnerUserId, trip.Status, trip.StartedOn, trip.EndedOn,
            trip.Title, trip.PlaceName,
            trip.Location is null ? null : new TripLocationModel(trip.Location.Latitude, trip.Location.Longitude,
                trip.Location.AccuracyMetres, trip.Location.CapturedOn, trip.Location.Source, trip.Location.Visibility, trip.Location.ConsentVersion),
            SyncStatus.Synchronised, DateTimeOffset.UtcNow,
            Photographs: [.. detail.Photographs.Select(photo => new TripPhotographModel(photo.Id, trip.Id,
                photo.ContributedByUserId, photo.ContentType, photo.AddedOn, photo.CapturedOn, SyncStatus: SyncStatus.Synchronised)
            { RemoteUrl = photo.Url })],
            Notes: [.. detail.Notes.Select(note => new TripNoteModel(note.Id, trip.Id, note.CreatedByUserId,
                note.Text, note.RecordedOn, SyncStatus.Synchronised))],
            PlannedStartOn: trip.PlannedStartOn);
        await TripStore.SaveAsync(local, cancellationToken);
        return local;
    }

    private async Task<IReadOnlyList<CatchModel>> LoadCatchesAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        try
        {
            var catches = await CatchStore.GetMetadataAsync(ownerUserId, cancellationToken);
            return
            [
                .. catches
                    .Where(catchRecord => catchRecord.TripId == TripId)
                    .OrderBy(catchRecord => catchRecord.CaughtOn)
            ];
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync(
                "reading the catches of a trip for editing",
                exception,
                CancellationToken.None);
            return [];
        }
    }

    private async Task ReadPreferencesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var preferences = await AnglerPreferences.GetAsync(cancellationToken);
            _weightUnit = preferences.WeightUnit;
            _lengthUnit = preferences.LengthUnit;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync(
                "reading angler preferences for a trip",
                exception,
                CancellationToken.None);
        }
    }

    private async Task RetryAsync()
    {
        if (_isLoading)
        {
            return;
        }

        await LoadAsync();
    }

    private void ReturnToDiary()
    {
        Navigation.NavigateTo($"/trips/{TripId:D}");
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
