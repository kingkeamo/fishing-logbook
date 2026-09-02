using System.Globalization;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Features.Catch.Clients;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Clients;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Catch.Components.CatchProvenanceEditor;

public partial class CatchProvenanceEditor : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private bool _isLoading = true;
    private bool _isOnline;
    private bool _isUpdating;
    private bool _updateFailed;
    private string? _updateErrorMessage;
    private string? _tripTitle;
    private string? _tripStartedOnLabel;
    private IReadOnlyList<CatchAnglerOptionModel> _anglerOptions = [];
    private Guid _persistedAnglerUserId;
    private Guid _selectedAnglerUserId;
    private string? _anglerName;
    private Guid _recordedByUserId;
    private string? _recordedByName;

    [Parameter, EditorRequired]
    public Guid CatchId { get; set; }

    [Parameter, EditorRequired]
    public Guid? TripId { get; set; }

    [Inject]
    private ICatchClient CatchClient { get; set; } = default!;

    [Inject]
    private ITripClient TripClient { get; set; } = default!;

    [Inject]
    private ITripParticipantClient ParticipantClient { get; set; } = default!;

    [Inject]
    private INetworkService Network { get; set; } = default!;

    [Inject]
    private ITimeService Time { get; set; } = default!;

    [Inject]
    private ILoggingService Logging { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private bool ShowRecordedBy => _recordedByName is not null && _recordedByUserId != _persistedAnglerUserId;

    private bool ShowAnglerPicker => _anglerOptions.Count > 1;

    private bool HasPendingSelection => _selectedAnglerUserId != _persistedAnglerUserId;

    protected override void OnInitialized()
    {
        Network.ConnectivityChanged += OnConnectivityChanged;
    }

    protected override async Task OnInitializedAsync()
    {
        if (TripId is not { } tripId)
        {
            _isLoading = false;
            return;
        }

        _isOnline = await Network.IsOnlineAsync(_cancellationTokenSource.Token);
        await LoadAsync(tripId, _cancellationTokenSource.Token);
        _isLoading = false;
    }

    private async Task LoadAsync(Guid tripId, CancellationToken cancellationToken)
    {
        if (!_isOnline)
        {
            return;
        }

        try
        {
            var catchView = await CatchClient.GetAsync(CatchId, cancellationToken);
            if (catchView is null)
            {
                return;
            }

            _persistedAnglerUserId = catchView.CaughtByUserId;
            _selectedAnglerUserId = catchView.CaughtByUserId;
            _anglerName = catchView.AnglerName;
            _recordedByUserId = catchView.RecordedByUserId;
            _recordedByName = catchView.RecordedByName;

            var detail = await TripClient.GetDetailAsync(tripId, cancellationToken);
            if (detail is not null)
            {
                _tripTitle = detail.Trip.Title;
                _tripStartedOnLabel = await FormatStartedOnAsync(detail.Trip.StartedOn, cancellationToken);
            }

            var participants = await ParticipantClient.GetAsync(tripId, cancellationToken);
            if (participants is not null)
            {
                _anglerOptions =
                [
                    .. participants.Participants
                        .Where(participant =>
                            participant.IsOwner || participant.Status == TripParticipantConstants.Accepted)
                        .Select(participant => new CatchAnglerOptionModel(
                            participant.UserId,
                            string.IsNullOrWhiteSpace(participant.DisplayName)
                                ? Loc["Trip_ContributorUnknown"].Value
                                : participant.DisplayName))
                ];
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("loading catch provenance", exception, CancellationToken.None);
        }
    }

    private async Task<string?> FormatStartedOnAsync(DateTimeOffset startedOn, CancellationToken cancellationToken)
    {
        var startedLocal = await Time.ToDateTimeLocalValueAsync(startedOn, cancellationToken);
        return DateTime.TryParseExact(
            startedLocal,
            "yyyy-MM-ddTHH:mm",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed)
            ? parsed.ToString("d MMM yyyy", CultureInfo.CurrentCulture)
            : null;
    }

    private void SelectAngler(Guid anglerUserId)
    {
        if (_isUpdating || !_isOnline)
        {
            return;
        }

        _selectedAnglerUserId = anglerUserId;
        _updateFailed = false;
    }

    private async Task UpdateAsync()
    {
        if (_isUpdating || !_isOnline || !HasPendingSelection)
        {
            return;
        }

        _isUpdating = true;
        _updateFailed = false;
        _updateErrorMessage = null;
        try
        {
            var result = await CatchClient.CorrectAnglerAsync(
                CatchId,
                _selectedAnglerUserId,
                _cancellationTokenSource.Token);
            if (result.Catch is null)
            {
                _updateFailed = true;
                _updateErrorMessage = result.ErrorMessage;
                return;
            }

            _persistedAnglerUserId = result.Catch.CaughtByUserId;
            _selectedAnglerUserId = result.Catch.CaughtByUserId;
            _anglerName = result.Catch.AnglerName;
            _recordedByUserId = result.Catch.RecordedByUserId;
            _recordedByName = result.Catch.RecordedByName;
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("correcting the catch angler", exception, CancellationToken.None);
            _updateFailed = true;
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private void OnConnectivityChanged(bool isOnline)
    {
        _ = UpdateConnectivityAsync(isOnline);
    }

    private async Task UpdateConnectivityAsync(bool isOnline)
    {
        var cancellationToken = _cancellationTokenSource.Token;
        try
        {
            await InvokeAsync(async () =>
            {
                var reconnected = isOnline && !_isOnline;
                _isOnline = isOnline;
                if (reconnected && TripId is { } tripId && _anglerOptions.Count == 0)
                {
                    await LoadAsync(tripId, cancellationToken);
                }

                StateHasChanged();
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await Logging.LogErrorAsync("handling a connectivity change", exception, CancellationToken.None);
        }
    }

    public void Dispose()
    {
        Network.ConnectivityChanged -= OnConnectivityChanged;
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
