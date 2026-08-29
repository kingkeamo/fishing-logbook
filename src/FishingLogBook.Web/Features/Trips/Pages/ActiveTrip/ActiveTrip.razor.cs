using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Profile.Providers;
using FishingLogBook.Web.Features.Trips.Clients;
using FishingLogBook.Web.Features.Trips.Enums;
using FishingLogBook.Web.Features.Trips.Modals.TripParticipants;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
using FishingLogBook.Web.Features.Trips.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Trips.Pages.ActiveTrip;

public partial class ActiveTrip : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private TripModel? _trip;
    private TripDisplayModel? _display;
    private bool _isLoading = true;
    private bool _loadFailed;
    private bool _isFinishing;
    private bool _locationAttempted;
    private int? _catchCount;
    private IReadOnlyList<TripTimelineItemModel> _timeline = [];
    private int? _photographCount;
    private int? _noteCount;
    private WeightUnitEnum _weightUnit = WeightUnitEnum.Kg;
    private LengthUnitEnum _lengthUnit = LengthUnitEnum.Cm;
    private bool _isReadOnlyHistory;
    private bool _isOnline = true;
    private Guid _viewerUserId;
    private IReadOnlyList<TripContributorDto> _contributors = [];
    private string _role = TripParticipantConstants.Owner;

    [Parameter]
    public Guid TripId { get; set; }

    [Inject]
    private ITripStore TripStore { get; set; } = default!;

    [Inject]
    private IActiveTripService ActiveTripService { get; set; } = default!;

    [Inject]
    private ITripDisplayService TripDisplay { get; set; } = default!;

    [Inject]
    private ILocalCatchOwnerService LocalOwner { get; set; } = default!;

    [Inject]
    private ICatchStore CatchStore { get; set; } = default!;

    [Inject]
    private ITripClient TripClient { get; set; } = default!;

    [Inject]
    private IModalService ModalService { get; set; } = default!;

    [Inject]
    private INetworkService Network { get; set; } = default!;

    [Inject]
    private ITripTimelineService TripTimeline { get; set; } = default!;

    [Inject]
    private IAnglerPreferencesProvider AnglerPreferences { get; set; } = default!;

    [Inject]
    private ILoggingService Logging { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private bool IsCompleted
    {
        get
        {
            return _trip?.Status == TripConstants.Completed;
        }
    }

    private bool IsOwner
    {
        get
        {
            return _role == TripParticipantConstants.Owner;
        }
    }

    private bool IsContributor
    {
        get
        {
            return _role is TripParticipantConstants.Owner or TripParticipantConstants.Participant;
        }
    }

    private bool CanManageTrip
    {
        get
        {
            return !IsCompleted && !_isReadOnlyHistory && IsOwner;
        }
    }

    private bool CanContribute
    {
        get
        {
            return IsContributor && (!_isReadOnlyHistory || _isOnline);
        }
    }

    private bool CanAddNotes => CanContribute;

    private bool CanAddCatches => CanContribute;

    private bool CanAddPhotographs => !IsCompleted && CanContribute;

    private bool CanRecordCatch => !IsCompleted && CanContribute;

    private TripStorageEnum NoteStorage
    {
        get
        {
            return _isReadOnlyHistory ? TripStorageEnum.Server : TripStorageEnum.LocalFirst;
        }
    }

    private string? GeneratedTitle
    {
        get
        {
            return _display?.StartedDate;
        }
    }

    private string? StartedLabel
    {
        get
        {
            return _display?.StartedTime;
        }
    }

    private string? DurationLabel
    {
        get
        {
            return _display?.Elapsed is null ? null : FormatDuration(_display.Elapsed.Value);
        }
    }

    protected override void OnInitialized()
    {
        Network.ConnectivityChanged += OnConnectivityChanged;
    }

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
        await RefreshConnectivityAsync();
    }

    private void OnConnectivityChanged(bool isOnline)
    {
        _ = UpdateConnectivityAsync(isOnline);
    }

    private async Task UpdateConnectivityAsync(bool isOnline)
    {
        if (_cancellationTokenSource.IsCancellationRequested)
        {
            return;
        }

        await InvokeAsync(() =>
        {
            _isOnline = isOnline;
            StateHasChanged();
        });
    }

    private async Task RefreshConnectivityAsync()
    {
        try
        {
            _isOnline = await Network.IsOnlineAsync(_cancellationTokenSource.Token);
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync(
                "reading connectivity for a trip",
                exception,
                CancellationToken.None);
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _locationAttempted || _trip is null || IsCompleted)
        {
            return;
        }

        _locationAttempted = true;
        await TryAttachLocationAsync();
    }

    private async Task<IReadOnlyList<CatchModel>?> LoadCatchesAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await CatchStore.GetMetadataAsync(ownerUserId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("reading the catches of a trip", exception, CancellationToken.None);
            return null;
        }
    }

    private async Task LoadAsync()
    {
        _isLoading = true;
        _loadFailed = false;
        var cancellationToken = _cancellationTokenSource.Token;
        try
        {
            var viewerUserId = await LocalOwner.GetUserIdAsync(cancellationToken);
            _viewerUserId = viewerUserId;
            _trip = await TripStore.GetAsync(viewerUserId, TripId, cancellationToken);
            TripDetailDto? shared = null;
            if (_trip is { Origin: TripOriginEnum.Server })
            {
                shared = await RefreshSharedTripAsync(viewerUserId, cancellationToken);
                _trip = await TripStore.GetAsync(viewerUserId, TripId, cancellationToken) ?? _trip;
            }

            if (_trip is not null)
            {
                _isReadOnlyHistory = false;
                _role = _trip.IsOwnedBy(viewerUserId)
                    ? TripParticipantConstants.Owner
                    : TripParticipantConstants.Participant;
                _display = await TripDisplay.DescribeAsync(_trip, cancellationToken);
                var catches = await LoadCatchesAsync(viewerUserId, cancellationToken);
                _timeline = shared is null
                    ? TripTimeline.BuildLocal(_trip, catches ?? [])
                    : TripTimeline.BuildShared(shared, _trip, catches ?? []);
                _catchCount = _timeline.Count(item => item.Kind == TripTimelineKindEnum.Catch);
                _photographCount = _timeline.Count(item => item.Kind == TripTimelineKindEnum.Photograph);
                _noteCount = _timeline.Count(item => item.Kind == TripTimelineKindEnum.Note);
                await LoadContributorsAsync(cancellationToken);
                await ReadPreferencesAsync(cancellationToken);
                return;
            }

            await LoadHistoricalAsync(viewerUserId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            _loadFailed = true;
            _trip = null;
            await Logging.LogErrorAsync("loading a trip", exception, CancellationToken.None);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task LoadHistoricalAsync(Guid viewerUserId, CancellationToken cancellationToken)
    {
        var detail = await TripClient.GetDetailAsync(TripId, cancellationToken);
        if (detail is null)
        {
            return;
        }

        _isReadOnlyHistory = true;
        _role = detail.Role;
        _contributors = detail.Contributors;
        _trip = ToTripModel(detail, ParticipantUserIds(detail, viewerUserId));
        _display = await TripDisplay.DescribeAsync(_trip, cancellationToken);
        _catchCount = detail.Catches.Count;
        _photographCount = detail.Photographs.Count;
        _noteCount = detail.Notes.Count;
        _timeline = TripTimeline.BuildRemote(detail);
        await ReadPreferencesAsync(cancellationToken);
        await HydrateSharedTripAsync(detail, viewerUserId, cancellationToken);
    }

    private async Task<TripDetailDto?> RefreshSharedTripAsync(
        Guid viewerUserId,
        CancellationToken cancellationToken)
    {
        try
        {
            var detail = await TripClient.GetDetailAsync(TripId, cancellationToken);
            if (detail is null)
            {
                return null;
            }

            _contributors = detail.Contributors;
            var refreshed = ToTripModel(detail, ParticipantUserIds(detail, viewerUserId));
            if (!refreshed.CanContribute(viewerUserId))
            {
                return detail;
            }

            await TripStore.HydrateAsync(
                ToHydratedTrip(detail, refreshed),
                viewerUserId,
                cancellationToken);
            return detail;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync(
                "refreshing a shared trip",
                exception,
                CancellationToken.None);
            return null;
        }
    }

    private async Task LoadContributorsAsync(CancellationToken cancellationToken)
    {
        if (_trip is null || _trip.ParticipantUserIds.Count == 0 || _contributors.Count > 0)
        {
            return;
        }

        try
        {
            var detail = await TripClient.GetDetailAsync(TripId, cancellationToken);
            if (detail is not null)
            {
                _contributors = detail.Contributors;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync(
                "reading the contributors of a trip",
                exception,
                CancellationToken.None);
        }
    }

    private async Task HydrateSharedTripAsync(
        TripDetailDto detail,
        Guid viewerUserId,
        CancellationToken cancellationToken)
    {
        if (_trip is null
            || detail.Trip.Status != TripConstants.Active
            || !_trip.CanContribute(viewerUserId))
        {
            return;
        }

        try
        {
            await TripStore.HydrateAsync(ToHydratedTrip(detail, _trip), viewerUserId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync(
                "caching a shared trip for offline use",
                exception,
                CancellationToken.None);
        }
    }

    private static TripModel ToHydratedTrip(TripDetailDto detail, TripModel trip)
    {
        return trip with
        {
            Origin = TripOriginEnum.Server,
            SyncedAt = DateTimeOffset.UtcNow,
            Notes =
            [
                .. detail.Notes.Select(note => new TripNoteModel(
                    note.Id,
                    detail.Trip.Id,
                    note.CreatedByUserId,
                    note.Text,
                    note.RecordedOn,
                    SyncStatus.Synchronised,
                    DateTimeOffset.UtcNow))
            ]
        };
    }

    private static IReadOnlyList<Guid> ParticipantUserIds(TripDetailDto detail, Guid viewerUserId)
    {
        if (detail.Role != TripParticipantConstants.Participant)
        {
            return [];
        }

        return [viewerUserId];
    }

    private static TripModel ToTripModel(TripDetailDto detail, IReadOnlyList<Guid> participantUserIds)
    {
        return new TripModel(
            detail.Trip.Id,
            detail.Trip.OwnerUserId,
            detail.Trip.Status,
            detail.Trip.StartedOn,
            detail.Trip.EndedOn,
            detail.Trip.Title,
            detail.Trip.PlaceName,
            SyncStatus: SyncStatus.Synchronised,
            ParticipantUserIds: participantUserIds,
            Origin: TripOriginEnum.Server);
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

    private async Task ShowParticipantsAsync()
    {
        var changed = await ModalService
            .ShowAsync<TripParticipantsModal, TripParticipantsModalModel, TripParticipantsModalResult>(
                new TripParticipantsModalModel(TripId),
                _cancellationTokenSource.Token);
        if (changed is null)
        {
            return;
        }

        await LoadAsync();
    }

    private async Task FinishAsync()
    {
        if (_trip is null || _isFinishing || IsCompleted)
        {
            return;
        }

        _isFinishing = true;
        var cancellationToken = _cancellationTokenSource.Token;
        try
        {
            _trip = await ActiveTripService.FinishAsync(_trip, cancellationToken);
            _display = await TripDisplay.DescribeAsync(_trip, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            _loadFailed = true;
            await Logging.LogErrorAsync("finishing a trip", exception, CancellationToken.None);
        }
        finally
        {
            _isFinishing = false;
        }
    }

    private async Task TryAttachLocationAsync()
    {
        var cancellationToken = _cancellationTokenSource.Token;
        try
        {
            var located = await ActiveTripService.TryAttachLocationAsync(_trip!, cancellationToken);
            if (located is null)
            {
                return;
            }

            _trip = located;
            StateHasChanged();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("attaching a trip location", exception, CancellationToken.None);
        }
    }

    private string FormatDuration(TimeSpan elapsed)
    {
        var hours = (int)elapsed.TotalHours;
        var minutes = elapsed.Minutes;
        return hours > 0
            ? Loc["Trip_DurationHoursMinutes", hours, minutes]
            : Loc["Trip_DurationMinutes", minutes];
    }

    public void Dispose()
    {
        Network.ConnectivityChanged -= OnConnectivityChanged;
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
