using Bunit;
using FishingLogBook.Web.Common.Modals.AnglerPicker;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Profile.Clients;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Common.Modals.AnglerPickerModalTests;

public class BaseAnglerPickerModalTest
{
    protected static BunitContext CreateContext(IProfileClient profileClient)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(profileClient);
        context.Services.AddSingleton(Substitute.For<ILoggingService>());
        context.Services.AddTransient<MudLocalizer, FishingLogBookMudLocalizer>();
        return context;
    }

    protected static async Task<(IRenderedComponent<MudDialogProvider> Cut, IDialogReference Dialog)> ShowAsync(
        BunitContext context,
        AnglerPickerModalModel? model = null)
    {
        var cut = context.Render<MudDialogProvider>();
        var dialogs = context.Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<AnglerPickerModal>
        {
            { modal => modal.Model, model ?? new AnglerPickerModalModel() }
        };
        var dialog = await dialogs.ShowAsync<AnglerPickerModal>(parameters, new DialogOptions());
        return (cut, dialog);
    }
}
