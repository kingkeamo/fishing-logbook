using System.Collections.ObjectModel;
using System.ComponentModel;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.Features.Trips.Clients;
using FishingLogBook.Mobile.Features.Trips.Models;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Mobile.Features.Trips.ViewModels;

/// <summary>The Web participants modal: who is on the trip, owner-only invite by search, and remove. Needs the server, as on Web.</summary>
public sealed class TripParticipantsViewModel : INotifyPropertyChanged
{
    private const int MinimumSearchLength = 3;

    private readonly ITripParticipantClient _participants;
    private readonly IAnglerLookupClient _lookup;
    private readonly IConnectivityService _connectivity;
    private readonly IAccountSessionCoordinator _session;
    private Guid _tripId;
    private string? _role;
    private string _searchText = string.Empty;
    private string? _messageKey;
    private bool _isLoading;
    private bool _isBusy;
    private int _loadVersion;

    public TripParticipantsViewModel(
        ITripParticipantClient participants,
        IAnglerLookupClient lookup,
        IConnectivityService connectivity,
        IAccountSessionCoordinator session,
        UiTextService text)
    {
        _participants = participants;
        _lookup = lookup;
        _connectivity = connectivity;
        _session = session;
        Text = text;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public UiTextService Text { get; }

    public ObservableCollection<TripParticipantItemModel> Participants { get; } = [];

    public ObservableCollection<AnglerSummaryDto> SearchResults { get; } = [];

    public bool Changed { get; private set; }

    public bool IsLoading => _isLoading;

    public bool CanInvite => _role == TripParticipantConstants.Owner;

    public bool ShowOwnerOnlyNote => !CanInvite && Participants.Count > 0;

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

    public bool CanSearch => !_isBusy && _searchText.Trim().Length >= MinimumSearchLength;

    public bool HasMessage => _messageKey is not null;

    public string MessageText => _messageKey is null ? string.Empty : Text[_messageKey];

    public async Task LoadAsync(Guid tripId, CancellationToken cancellationToken)
    {
        var version = ++_loadVersion;
        _tripId = tripId;
        Changed = false;
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

        _messageKey = null;
        SearchResults.Clear();
        var response = await _lookup.FindAsync(_searchText.Trim(), cancellationToken);
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

        await ChangeAsync(() => _participants.InviteAsync(_tripId, userId, cancellationToken), "TripInviteFailed");
        SearchResults.Clear();
        SearchText = string.Empty;
    }

    public async Task RemoveAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (!CanInvite)
        {
            return;
        }

        await ChangeAsync(() => _participants.RemoveAsync(_tripId, userId, cancellationToken), "TripParticipantsActionFailed");
    }

    private async Task ChangeAsync(Func<Task<Api.Models.ApiResultModel<TripParticipantsDto>>> change, string failureKey)
    {
        if (_isBusy)
        {
            return;
        }

        _isBusy = true;
        Notify();
        try
        {
            var response = await change();
            _messageKey = response.IsSuccess ? null : failureKey;
            if (response.IsSuccess)
            {
                Apply(response.Value!);
                Changed = true;
            }
        }
        finally
        {
            _isBusy = false;
            Notify();
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
