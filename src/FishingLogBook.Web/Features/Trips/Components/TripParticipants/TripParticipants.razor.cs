using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Common.Modals.AnglerPicker;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Clients;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace FishingLogBook.Web.Features.Trips.Components.TripParticipants;

public partial class TripParticipants : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private IReadOnlyList<TripParticipantDto> _participants = [];
    private string _role = TripParticipantConstants.None;
    private bool _isLoading = true;
    private bool _loadFailed;
    private bool _actionFailed;
    private bool _isBusy;

    [Parameter]
    public Guid TripId { get; set; }

    [Parameter]
    public bool Enabled { get; set; } = true;

    [Parameter]
    public bool AllowManagement { get; set; } = true;

    [Parameter]
    public EventCallback<TripParticipantsDto> Changed { get; set; }

    private Guid? _loadedTripId;

    [Inject]
    private ITripParticipantClient ParticipantClient { get; set; } = default!;

    [Inject]
    private IModalService ModalService { get; set; } = default!;

    [Inject]
    private ILoggingService Logging { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private bool IsOwner
    {
        get
        {
            return Enabled && AllowManagement && _role == TripParticipantConstants.Owner;
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (_loadedTripId != TripId && Enabled)
        {
            _loadedTripId = TripId;
            await LoadAsync();
        }
    }

    private string DisplayName(TripParticipantDto participant)
    {
        return string.IsNullOrWhiteSpace(participant.DisplayName)
            ? Loc["Trip_ContributorUnknown"].Value
            : participant.DisplayName;
    }

    private string StatusLabel(TripParticipantDto participant)
    {
        if (participant.IsOwner)
        {
            return Loc["Trip_ParticipantOwner"].Value;
        }

        return participant.Status switch
        {
            TripParticipantConstants.Pending => Loc["Trip_ParticipantPending"].Value,
            TripParticipantConstants.Declined => Loc["Trip_ParticipantDeclined"].Value,
            _ => Loc["Trip_ParticipantAccepted"].Value
        };
    }

    private bool CanRemove(TripParticipantDto participant)
    {
        return IsOwner && !participant.IsOwner;
    }

    private async Task LoadAsync()
    {
        _isLoading = true;
        _loadFailed = false;
        try
        {
            var participants = await ParticipantClient.GetAsync(
                TripId,
                _cancellationTokenSource.Token);
            if (participants is null)
            {
                _loadFailed = true;
                return;
            }

            Apply(participants);
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _loadFailed = true;
            await Logging.LogErrorAsync("loading trip participants", exception, CancellationToken.None);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task InviteAsync()
    {
        if (!IsOwner || _isBusy)
        {
            return;
        }
        _isBusy = true;
        _actionFailed = false;
        try
        {
            var selected = await ModalService
                .ShowAsync<AnglerPickerModal, AnglerPickerModalModel, AnglerPickerModalResult>(
                    new AnglerPickerModalModel(_participants.Select(participant => participant.UserId).ToArray()),
                    _cancellationTokenSource.Token);
            if (selected is null)
            {
                return;
            }
            var participants = await ParticipantClient.InviteAsync(TripId,
                new InviteTripParticipantDto(selected.Angler.UserId), _cancellationTokenSource.Token);
            if (participants is null)
            {
                _actionFailed = true;
                return;
            }
            Apply(participants);
            await Changed.InvokeAsync(participants);
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _actionFailed = true;
            await Logging.LogErrorAsync("inviting an angler to a trip", exception, CancellationToken.None);
        }
        finally
        {
            _isBusy = false;
        }
    }
    private async Task RemoveAsync(Guid participantUserId)
    {
        if (_isBusy || !IsOwner || !_participants.Any(participant => participant.UserId == participantUserId && !participant.IsOwner))
        {
            return;
        }

        var confirmed = await ModalService.ConfirmAsync(
            new ConfirmModalModel(
                Loc["Trip_RemoveParticipantTitle"].Value,
                Loc["Trip_RemoveParticipantMessage"].Value,
                Loc["Trip_RemoveParticipant"].Value,
                Loc["Modal_Cancel"].Value,
                IsDestructive: true),
            _cancellationTokenSource.Token);
        if (!confirmed)
        {
            return;
        }

        _isBusy = true;
        _actionFailed = false;
        try
        {
            var participants = await ParticipantClient.RemoveAsync(
                TripId,
                participantUserId,
                _cancellationTokenSource.Token);
            if (participants is null)
            {
                _actionFailed = true;
                return;
            }

            Apply(participants);
            await Changed.InvokeAsync(participants);
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _actionFailed = true;
            await Logging.LogErrorAsync("removing a trip participant", exception, CancellationToken.None);
        }
        finally
        {
            _isBusy = false;
        }
    }

    private void Apply(TripParticipantsDto participants)
    {
        _participants = participants.Participants;
        _role = participants.Role;
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
