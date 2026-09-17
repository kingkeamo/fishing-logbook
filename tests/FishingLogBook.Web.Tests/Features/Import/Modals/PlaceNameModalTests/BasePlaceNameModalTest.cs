using Bunit;
using FishingLogBook.Web.Features.Import.Modals.PlaceName;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace FishingLogBook.Web.Tests.Features.Import.Modals.PlaceNameModalTests;

public class BasePlaceNameModalTest
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
        PlaceNameModalModel model)
    {
        var cut = context.Render<MudDialogProvider>();
        var dialogs = context.Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<PlaceNameModal>
        {
            { modal => modal.Model, model }
        };
        var dialog = await dialogs.ShowAsync<PlaceNameModal>(string.Empty, parameters);
        return (cut, dialog);
    }

    protected static PlaceNameModalModel DefaultModel(string? currentPlaceName = null)
    {
        return new PlaceNameModalModel(
            ["42 Street 7", "Zayed International Airport", "Abu Dhabi", "United Arab Emirates"],
            currentPlaceName);
    }
}
