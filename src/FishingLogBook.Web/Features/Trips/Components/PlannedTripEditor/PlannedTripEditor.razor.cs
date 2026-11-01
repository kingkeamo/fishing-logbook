using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Modals.TripParticipants;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
using FishingLogBook.Web.Features.Trips.Offline.Synchronisers;
using FishingLogBook.Web.Features.Trips.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Trips.Components.PlannedTripEditor;

public partial class PlannedTripEditor : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private TripModel? _trip;
    private string? _title;
    private string? _placeName;
    private string _whenLocal = string.Empty;
    private bool _whenInvalid;
    private bool _saveFailed;
    private bool _isBusy;
    private bool _isOnline;

    [Parameter]
    public Guid? TripId { get; set; }

    [Parameter]
    public Guid ViewerUserId { get; set; }

    [Parameter]
    public string TripBaseHref { get; set; } = "/trips";

    [Parameter]
    public EventCallback OnClosed { get; set; }

    [Inject]
    private ITripStore TripStore { get; set; } = default!;

    [Inject]
    private IPlannedTripService PlannedTrips { get; set; } = default!;

    [Inject]
    private ITripSynchroniser TripSynchroniser { get; set; } = default!;

    [Inject]
    private ITimeService Time { get; set; } = default!;

    [Inject]
    private INetworkService Network { get; set; } = default!;

    [Inject]
    private IModalService ModalService { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private ILoggingService Logging { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private bool CanEdit
    {
        get
        {
            return _trip is null || (_trip.IsPlanned && _trip.IsOwnedBy(ViewerUserId));
        }
    }

    private bool CanInvite
    {
        get
        {
            return _trip is { SyncStatus: SyncStatus.Synchronised } && _isOnline;
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        var cancellationToken = _cancellationTokenSource.Token;
        _isOnline = await Network.IsOnlineAsync(cancellationToken);
        if (TripId is not { } tripId)
        {
            return;
        }

        _trip = await TripStore.GetAsync(ViewerUserId, tripId, cancellationToken);
        if (_trip is null)
        {
            return;
        }

        _title = _trip.Title;
        _placeName = _trip.PlaceName;
        _whenLocal = _trip.PlannedStartOn is { } planned
            ? await Time.ToDateTimeLocalValueAsync(planned, cancellationToken)
            : string.Empty;
    }

    private void OnPlaceNameChanged(string? placeName)
    {
        _placeName = placeName;
    }

    private void OnWhenChanged(string value)
    {
        _whenLocal = value;
        _whenInvalid = false;
    }

    private async Task SaveAsync()
    {
        var cancellationToken = _cancellationTokenSource.Token;
        _saveFailed = false;
        var plannedStartOn = string.IsNullOrWhiteSpace(_whenLocal)
            ? null
            : await Time.FromDateTimeLocalValueAsync(_whenLocal, cancellationToken);
        _whenInvalid = plannedStartOn is null;
        if (plannedStartOn is null)
        {
            return;
        }

        _isBusy = true;
        try
        {
            var saved = _trip is null
                ? await PlannedTrips.PlanAsync(ViewerUserId, _title, _placeName, plannedStartOn.Value, cancellationToken)
                : await PlannedTrips.UpdatePlanAsync(
                    ViewerUserId,
                    _trip.Id,
                    _title,
                    _placeName,
                    plannedStartOn.Value,
                    cancellationToken);
            if (saved is null)
            {
                _saveFailed = true;
                return;
            }

            await TrySynchroniseAsync(cancellationToken);
            if (_trip is null)
            {
                Navigation.NavigateTo($"{TripBaseHref.TrimEnd('/')}/{saved.Id:D}/plan");
                return;
            }

            _trip = await TripStore.GetAsync(ViewerUserId, saved.Id, cancellationToken) ?? saved;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _saveFailed = true;
            await Logging.LogErrorAsync("saving a planned trip", exception, CancellationToken.None);
        }
        finally
        {
            _isBusy = false;
        }
    }

    private async Task CancelTripAsync()
    {
        if (_trip is null)
        {
            return;
        }

        var confirmed = await ModalService.ConfirmAsync(
            new ConfirmModalModel(
                Loc["Trip_PlanCancelConfirmTitle"].Value,
                Loc["Trip_PlanCancelConfirmMessage"].Value,
                Loc["Trip_PlanCancelConfirm"].Value,
                Loc["Trip_PlanKeep"].Value),
            CancellationToken.None);
        if (!confirmed)
        {
            return;
        }

        var cancellationToken = _cancellationTokenSource.Token;
        _isBusy = true;
        try
        {
            var cancelled = await PlannedTrips.CancelPlanAsync(ViewerUserId, _trip.Id, cancellationToken);
            if (cancelled is null)
            {
                _saveFailed = true;
                return;
            }

            await TrySynchroniseAsync(cancellationToken);
            await OnClosed.InvokeAsync();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _saveFailed = true;
            await Logging.LogErrorAsync("cancelling a planned trip", exception, CancellationToken.None);
        }
        finally
        {
            _isBusy = false;
        }
    }

    private async Task InviteAsync()
    {
        if (_trip is null)
        {
            return;
        }

        await ModalService
            .ShowAsync<TripParticipantsModal, TripParticipantsModalModel, TripParticipantsModalResult>(
                new TripParticipantsModalModel(_trip.Id),
                CancellationToken.None);
    }

    private async Task CloseAsync()
    {
        await OnClosed.InvokeAsync();
    }

    private async Task TrySynchroniseAsync(CancellationToken cancellationToken)
    {
        try
        {
            await TripSynchroniser.SynchronisePendingAsync(ViewerUserId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("synchronising a planned trip", exception, CancellationToken.None);
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
