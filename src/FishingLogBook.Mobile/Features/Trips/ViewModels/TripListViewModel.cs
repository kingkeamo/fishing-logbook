using System.Collections.ObjectModel;
using System.ComponentModel;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.Features.Trips.Clients;
using FishingLogBook.Mobile.Features.Trips.Models;
using FishingLogBook.Mobile.Features.Trips.Services;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Mobile.Navigation;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Trips.ViewModels;

/// <summary>
/// The Trips tab. Mirrors the Web trip list: invitations first, then every trip newest first, with the
/// server history overlaid by trips and edits that still only exist on this device.
/// </summary>
public sealed class TripListViewModel : INotifyPropertyChanged
{
    private readonly ITripService _trips;
    private readonly ITripParticipantClient _participants;
    private readonly ITripStartService _start;
    private readonly IConnectivityService _connectivity;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private bool _isLoading = true;
    private bool _canStart;
    private string? _noticeKey;
    private string? _actionFailedKey;
    private bool _isBusy;

    public TripListViewModel(
        ITripService trips,
        ITripParticipantClient participants,
        ITripStartService start,
        IConnectivityService connectivity,
        UiTextService text,
        TimeProvider clock)
    {
        _trips = trips;
        _participants = participants;
        _start = start;
        _connectivity = connectivity;
        _clock = clock;
        Text = text;
        text.PropertyChanged += OnCultureChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public UiTextService Text { get; }

    public ObservableCollection<TripListItemModel> Trips { get; } = [];

    public ObservableCollection<TripInvitationItemModel> Invitations { get; } = [];

    public bool IsLoading
    {
        get => _isLoading;
        private set => Set(ref _isLoading, value, nameof(IsLoading), nameof(IsEmpty), nameof(CanInteract));
    }

    public bool IsEmpty => !IsLoading && Trips.Count == 0;

    public bool CanInteract => !IsLoading && !_isBusy;

    public bool HasInvitations => Invitations.Count > 0;

    public bool CanStart
    {
        get => _canStart;
        private set => Set(ref _canStart, value, nameof(CanStart));
    }

    public bool HasNotice => _noticeKey is not null;

    public string NoticeText => _noticeKey is null ? string.Empty : Text[_noticeKey];

    public bool HasActionFailure => _actionFailedKey is not null;

    public string ActionFailureText => _actionFailedKey is null ? string.Empty : Text[_actionFailedKey];

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        await _loadGate.WaitAsync(cancellationToken);
        try
        {
            IsLoading = Trips.Count == 0;
            var history = await _trips.LoadAsync(cancellationToken);
            var summaries = history.Items;
            Replace(Trips, summaries.Select(summary => ToItem(summary, history.PendingIds.Contains(summary.Id))));
            CanStart = !summaries.Any(summary => summary.Status == TripConstants.Active && summary.Role == TripParticipantConstants.Owner);
            _noticeKey = !history.ServerUnavailable ? null : _connectivity.IsOnline ? "TripListLoadFailed" : "TripListOffline";
            Replace(Invitations, history.ServerUnavailable ? [] : await LoadInvitationsAsync(cancellationToken));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            IsLoading = false;
            _loadGate.Release();
            Notify(nameof(IsEmpty), nameof(HasNotice), nameof(NoticeText), nameof(HasInvitations));
        }
    }

    /// <summary>Starts a trip on this device (works offline) and returns its id, or null when it failed.</summary>
    public async Task<Guid?> StartAsync(CancellationToken cancellationToken)
    {
        if (!CanStart || _isBusy)
        {
            return null;
        }

        SetBusy(true);
        try
        {
            return await _start.StartAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _actionFailedKey = "TripPlannedStartFailed";
            Notify(nameof(HasActionFailure), nameof(ActionFailureText));
            await LoadAsync(cancellationToken);
            return null;
        }
        finally
        {
            SetBusy(false);
        }
    }

    public Task AcceptAsync(Guid tripId, CancellationToken cancellationToken)
    {
        return RespondAsync(tripId, accept: true, cancellationToken);
    }

    public Task DeclineAsync(Guid tripId, CancellationToken cancellationToken)
    {
        return RespondAsync(tripId, accept: false, cancellationToken);
    }

    private async Task RespondAsync(Guid tripId, bool accept, CancellationToken cancellationToken)
    {
        if (_isBusy)
        {
            return;
        }

        SetBusy(true);
        try
        {
            var result = await _participants.RespondAsync(tripId, accept, cancellationToken);
            _actionFailedKey = result.IsSuccess ? null : "TripParticipantsActionFailed";
            Notify(nameof(HasActionFailure), nameof(ActionFailureText));
            if (result.IsSuccess)
            {
                await LoadAsync(cancellationToken);
            }
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task<IReadOnlyList<TripInvitationItemModel>> LoadInvitationsAsync(CancellationToken cancellationToken)
    {
        if (!_connectivity.IsOnline)
        {
            return [];
        }

        var response = await _participants.ListInvitationsAsync(cancellationToken);
        return response.Value?.Select(ToInvitation).ToArray() ?? [];
    }

    private TripInvitationItemModel ToInvitation(TripInvitationDto invitation)
    {
        var inviter = string.IsNullOrWhiteSpace(invitation.OwnerDisplayName) ? Text["TripContributorUnknown"] : invitation.OwnerDisplayName;
        var started = invitation.StartedOn.ToLocalTime().ToString("d", Text.Culture);
        var place = string.IsNullOrWhiteSpace(invitation.PlaceName) ? invitation.Title : invitation.PlaceName;
        return new TripInvitationItemModel(
            invitation.TripId,
            string.Format(Text.Culture, Text["TripInvitationFrom"], inviter),
            string.IsNullOrWhiteSpace(place) ? started : $"{place} · {started}",
            Text["TripInvitationAccept"],
            Text["TripInvitationDecline"]);
    }

    private TripListItemModel ToItem(TripSummaryDto trip, bool pending)
    {
        var active = trip.Status == TripConstants.Active;
        var plannedLifecycle = TripConstants.HasPlannedLifecycle(trip.Status);
        return new TripListItemModel(
            trip.Id,
            TimeZoneInfo.ConvertTime(plannedLifecycle ? trip.PlannedStartOn ?? trip.StartedOn : trip.StartedOn, _clock.LocalTimeZone).ToString("d MMM yyyy", Text.Culture),
            trip.Title,
            trip.PlaceName,
            trip.Role == TripParticipantConstants.Participant,
            active,
            pending,
            Count(trip.CatchCount, "TripCatchesNone", "TripCatchesOne", "TripCatchesMany"),
            Count(trip.PhotographCount, "TripListPhotographsNone", "TripListPhotographsOne", "TripListPhotographsMany"),
            Count(trip.NoteCount, "TripListNotesNone", "TripListNotesOne", "TripListNotesMany"),
            plannedLifecycle ? Text["TripPlanOpen"] : active && trip.Role == TripParticipantConstants.Owner ? Text["TripUpdate"] : Text["TripViewTrip"],
            Text["TripActiveLabel"],
            Text["TripSharedLabel"],
            Text["CatchSyncWaiting"],
            plannedLifecycle,
            trip.Status == TripConstants.Cancelled ? Text["TripCancelled"] : Text["TripPlanned"],
            trip.Role == TripParticipantConstants.Owner);
    }

    public MobileRouteEnum RouteFor(Guid tripId)
    {
        return Trips.FirstOrDefault(trip => trip.Id == tripId) switch
        {
            { HasPlannedLifecycle: true } => MobileRouteEnum.TripPlan,
            { IsActive: true, CanUpdate: true } => MobileRouteEnum.TripEdit,
            _ => MobileRouteEnum.TripDetail
        };
    }

    private string Count(int count, string none, string one, string many)
    {
        return count switch
        {
            0 => Text[none],
            1 => Text[one],
            _ => string.Format(Text.Culture, Text[many], count)
        };
    }

    private void OnCultureChanged(object? sender, PropertyChangedEventArgs args)
    {
        Notify(nameof(NoticeText), nameof(ActionFailureText));
    }

    private void SetBusy(bool busy)
    {
        _isBusy = busy;
        Notify(nameof(CanInteract));
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }

    private void Set<T>(ref T field, T value, params string[] names)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        Notify(names);
    }

    private void Notify(params string[] names)
    {
        foreach (var name in names)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
