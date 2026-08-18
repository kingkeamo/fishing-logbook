using Bunit;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace FishingLogBook.Web.Tests.Common.Modals.ConfirmModalTests;

public class BaseConfirmModalTest
{
    protected static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        return context;
    }

    protected static async Task<(IRenderedComponent<MudDialogProvider> Cut, IDialogReference Dialog)> ShowAsync(
        BunitContext context,
        ConfirmModalModel model)
    {
        var cut = context.Render<MudDialogProvider>();
        var dialogs = context.Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<ConfirmModal>
        {
            { modal => modal.Model, model }
        };
        var dialog = await dialogs.ShowAsync<ConfirmModal>(
            model.Title,
            parameters,
            new DialogOptions
            {
                CloseButton = true,
                CloseOnEscapeKey = true,
                FullWidth = true,
                MaxWidth = MaxWidth.Small
            });
        return (cut, dialog);
    }

    protected static ConfirmModalModel DefaultModel(bool isDestructive = false)
    {
        return new ConfirmModalModel(
            "Leave without saving?",
            "Unsaved changes will be lost.",
            "Leave",
            "Stay",
            isDestructive);
    }
}
