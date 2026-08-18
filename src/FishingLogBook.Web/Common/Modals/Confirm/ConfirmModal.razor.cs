using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FishingLogBook.Web.Common.Modals;

public partial class ConfirmModal : ComponentBase
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    [Parameter]
    public ConfirmModalModel Model { get; set; } = default!;

    private void Confirm()
    {
        MudDialog.Close(DialogResult.Ok(true));
    }

    private void Cancel()
    {
        MudDialog.Cancel();
    }
}
