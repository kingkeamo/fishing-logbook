using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Clients;
using FishingLogBook.Web.Features.Trips.Modals.InviteAngler;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace FishingLogBook.Web.Features.Trips.Modals.TripParticipants;

public partial class TripParticipantsModal : ComponentBase, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private IReadOnlyList<TripParticipantDto> _participants = [];
    private string _role = TripParticipantConstants.None;
    private bool _isLoading = true;
    private bool _loadFailed;
    private bool _actionFailed;
    private bool _isBusy;
    private bool _changed;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    [Parameter]
    public TripParticipantsModalModel Model { get; set; } = default!;

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
            return _role == TripParticipantConstants.Owner;
        }
    }

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
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
                Model.TripId,
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
        _actionFailed = false;
        var invited = await ModalService
            .ShowAsync<InviteAnglerModal, InviteAnglerModalModel, InviteAnglerModalResult>(
                new InviteAnglerModalModel(Model.TripId),
                _cancellationTokenSource.Token);
        if (invited is null)
        {
            return;
        }

        Apply(invited.Participants);
        _changed = true;
    }

    private async Task RemoveAsync(Guid participantUserId)
    {
        if (_isBusy)
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
                Model.TripId,
                participantUserId,
                _cancellationTokenSource.Token);
            if (participants is null)
            {
                _actionFailed = true;
                return;
            }

            Apply(participants);
            _changed = true;
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

    private void Close()
    {
        if (_changed)
        {
            MudDialog.Close(DialogResult.Ok(new TripParticipantsModalResult(
                new TripParticipantsDto(Model.TripId, _role) { Participants = _participants })));
            return;
        }

        MudDialog.Cancel();
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
