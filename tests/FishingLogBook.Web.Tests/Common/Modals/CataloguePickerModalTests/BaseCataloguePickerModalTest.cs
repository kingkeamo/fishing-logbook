using Bunit;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace FishingLogBook.Web.Tests.Common.Modals.CataloguePickerModalTests;

public class BaseCataloguePickerModalTest
{
    protected static readonly Guid BrownTroutSpeciesId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    protected static readonly Guid PikeSpeciesId = Guid.Parse("cccccccc-0000-0000-0000-000000000002");
    protected static readonly Guid TenchSpeciesId = Guid.Parse("cccccccc-0000-0000-0000-000000000003");

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
        CataloguePickerModalModel model)
    {
        var cut = context.Render<MudDialogProvider>();
        var dialogs = context.Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<CataloguePickerModal>
        {
            { modal => modal.Model, model }
        };
        var dialog = await dialogs.ShowAsync<CataloguePickerModal>(
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

    protected static CataloguePickerModalModel DefaultModel()
    {
        return new CataloguePickerModalModel(
            "Species",
            [
                new CatalogueOptionModel(BrownTroutSpeciesId, "BrownTrout", "Brown Trout"),
                new CatalogueOptionModel(PikeSpeciesId, "Pike", "Pike"),
                new CatalogueOptionModel(TenchSpeciesId, "Tench", "Tench")
            ]);
    }
}
