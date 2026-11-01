using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace FishingLogBook.Web.Features.Trips.Modals.TripParticipants;

public partial class TripParticipantsModal : ComponentBase
{
    private TripParticipantsDto? _changed;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    [Parameter]
    public TripParticipantsModalModel Model { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private void OnChanged(TripParticipantsDto participants)
    {
        _changed = participants;
    }

    private void Close()
    {
        if (_changed is not null)
        {
            MudDialog.Close(DialogResult.Ok(new TripParticipantsModalResult(_changed)));
            return;
        }
        MudDialog.Cancel();
    }
}
