using System.Collections.ObjectModel;
using System.ComponentModel;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.Features.Trips.Clients;
using FishingLogBook.Mobile.Features.Trips.Models;
using FishingLogBook.Mobile.Interactions;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Trips.ViewModels;

/// <summary>
/// The Web participants modal: who is on the trip, owner-only invite by search, and remove. Needs the server, as on Web.
/// Nothing from an earlier trip or a failed load survives a load, and search, invite and remove only work once the
/// current trip has loaded.
/// </summary>
public sealed class TripParticipantsViewModel : INotifyPropertyChanged
{
    private const int MinimumSearchLength = 3;

    private readonly ITripParticipantClient _participants;
    private readonly IAnglerLookupClient _lookup;
    private readonly IConnectivityService _connectivity;
    private readonly IAccountSessionCoordinator _session;
    private readonly InteractionService _interactions;
    private Guid _tripId;
    private string? _role;
    private string _searchText = string.Empty;
    private string? _messageKey;
    private bool _isLoading;
    private bool _isLoaded;
    private bool _isBusy;
    private int _loadVersion;

    public TripParticipantsViewModel(
        ITripParticipantClient participants,
        IAnglerLookupClient lookup,
        IConnectivityService connectivity,
        IAccountSessionCoordinator session,
        InteractionService interactions,
        UiTextService text)
    {
        _participants = participants;
        _lookup = lookup;
        _connectivity = connectivity;
        _session = session;
        _interactions = interactions;
        Text = text;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public UiTextService Text { get; }

    public ObservableCollection<TripParticipantItemModel> Participants { get; } = [];

    public ObservableCollection<AnglerSummaryDto> SearchResults { get; } = [];

    public bool Changed { get; private set; }

    public bool IsLoading => _isLoading;

    public bool IsLoaded => _isLoaded;

    public bool CanInvite => _isLoaded && _role == TripParticipantConstants.Owner;

    public bool ShowOwnerOnlyNote => _isLoaded && !CanInvite && Participants.Count > 0;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value)
            {
                return;
            }

            _searchText = value;
            Notify();
        }
    }

    public bool CanSearch => _isLoaded && !_isBusy && _searchText.Trim().Length >= MinimumSearchLength;

    public bool HasMessage => _messageKey is not null;

    public string MessageText => _messageKey is null ? string.Empty : Text[_messageKey];

    public async Task LoadAsync(Guid tripId, CancellationToken cancellationToken)
    {
        var version = ++_loadVersion;
        _tripId = tripId;
        Changed = false;
        _isLoaded = false;
        _isBusy = false;
        _role = null;
        _searchText = string.Empty;
        Participants.Clear();
        SearchResults.Clear();
        _isLoading = true;
        _messageKey = null;
        Notify();
        try
        {
            if (!_connectivity.IsOnline)
            {
                _messageKey = "TripParticipantsLoadFailed";
                return;
            }

            var response = await _participants.GetAsync(tripId, cancellationToken);
            if (version != _loadVersion)
            {
                return;
            }

            if (response.IsSuccess)
            {
                Apply(response.Value!);
                _isLoaded = true;
            }
            else
            {
                _messageKey = "TripParticipantsLoadFailed";
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            if (version == _loadVersion)
            {
                _messageKey = "TripParticipantsLoadFailed";
            }
        }
        finally
        {
            if (version == _loadVersion)
            {
                _isLoading = false;
                Notify();
            }
        }
    }

    public async Task SearchAsync(CancellationToken cancellationToken)
    {
        if (!CanSearch)
        {
            return;
        }

        var version = _loadVersion;
        _messageKey = null;
        SearchResults.Clear();
        var response = await _lookup.FindAsync(_searchText.Trim(), cancellationToken);
        if (version != _loadVersion)
        {
            return;
        }

        var known = Participants.Select(participant => participant.UserId).ToHashSet();
        var self = _session.Capture()?.UserId ?? Guid.Empty;
        if (!response.IsSuccess)
        {
            _messageKey = "TripInviteSearchFailed";
        }
        else
        {
            foreach (var angler in response.Value!.Where(angler => !known.Contains(angler.UserId) && angler.UserId != self))
            {
                SearchResults.Add(angler);
            }

            _messageKey = SearchResults.Count == 0 ? "TripInviteNoResults" : null;
        }

        Notify();
    }

    public async Task InviteAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (!CanInvite)
        {
            return;
        }

        var version = _loadVersion;
        await ChangeAsync(() => _participants.InviteAsync(_tripId, userId, cancellationToken), "TripInviteFailed");
        if (version == _loadVersion)
        {
            SearchResults.Clear();
            SearchText = string.Empty;
        }
    }

    public async Task RemoveAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (!CanInvite)
        {
            return;
        }

        await ChangeAsync(() => _participants.RemoveAsync(_tripId, userId, cancellationToken), "TripParticipantsActionFailed");
    }

    /// <summary>Asks the angler to confirm, then removes the participant. A declined confirmation changes nothing.</summary>
    public async Task ConfirmAndRemoveAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (!CanInvite || _isBusy)
        {
            return;
        }

        var version = _loadVersion;
        var confirmed = await _interactions.ConfirmAsync(new DialogRequest(
            Text["TripRemoveParticipantTitle"], Text["TripRemoveParticipantMessage"], Text["TripRemoveParticipant"], Text["Cancel"], true), cancellationToken);
        if (confirmed && version == _loadVersion)
        {
            await RemoveAsync(userId, cancellationToken);
        }
    }

    private async Task ChangeAsync(Func<Task<Api.Models.ApiResultModel<TripParticipantsDto>>> change, string failureKey)
    {
        if (_isBusy)
        {
            return;
        }

        var version = _loadVersion;
        _isBusy = true;
        Notify();
        try
        {
            var response = await change();
            if (version != _loadVersion)
            {
                return;
            }

            _messageKey = response.IsSuccess ? null : failureKey;
            if (response.IsSuccess)
            {
                Apply(response.Value!);
                Changed = true;
            }
        }
        finally
        {
            if (version == _loadVersion)
            {
                _isBusy = false;
                Notify();
            }
        }
    }

    private void Apply(TripParticipantsDto dto)
    {
        _role = dto.Role;
        Participants.Clear();
        foreach (var participant in dto.Participants)
        {
            Participants.Add(new TripParticipantItemModel(
                participant.UserId,
                string.IsNullOrWhiteSpace(participant.DisplayName) ? Text["TripContributorUnknown"] : participant.DisplayName,
                participant.IsOwner ? Text["TripParticipantOwner"] : participant.Status switch
                {
                    TripParticipantConstants.Pending => Text["TripParticipantPending"],
                    TripParticipantConstants.Declined => Text["TripParticipantDeclined"],
                    _ => Text["TripParticipantAccepted"]
                },
                dto.Role == TripParticipantConstants.Owner && !participant.IsOwner));
        }

        Notify();
    }

    private void Notify()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }
}
