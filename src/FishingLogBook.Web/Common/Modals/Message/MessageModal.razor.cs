using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FishingLogBook.Web.Common.Modals;

public partial class MessageModal : ComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    [Parameter]
    public MessageModalModel Model { get; set; } = default!;

    private Severity AlertSeverity => Model.Severity switch
    {
        ModalSeverity.Warning => Severity.Warning,
        ModalSeverity.Error => Severity.Error,
        _ => Severity.Info
    };

    private void Close()
    {
        MudDialog.Close(DialogResult.Ok(true));
    }
}
