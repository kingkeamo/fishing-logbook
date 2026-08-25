using Bunit;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Features.Catch.Components.MeasurementEditor;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace FishingLogBook.Web.Tests.Features.Catch.Components.MeasurementEditorTests;

public class BaseMeasurementEditorTest
{
    protected static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton<IMeasurementService, MeasurementService>();
        context.Services.AddTransient<MudLocalizer, FishingLogBookMudLocalizer>();
        return context;
    }

    protected static async Task<(IRenderedComponent<MudDialogProvider> Cut, IDialogReference Dialog)> ShowAsync(
        BunitContext context,
        MeasurementEditorModel model)
    {
        var cut = context.Render<MudDialogProvider>();
        var dialogs = context.Services.GetRequiredService<IDialogService>();
        var parameters = new DialogParameters<MeasurementEditorModal>
        {
            { modal => modal.Model, model }
        };
        var dialog = await dialogs.ShowAsync<MeasurementEditorModal>(parameters);
        return (cut, dialog);
    }

    protected static MeasurementEditorModel Weight(
        decimal? canonicalValue = null,
        WeightUnitEnum unit = WeightUnitEnum.Kg)
    {
        return new MeasurementEditorModel(true, canonicalValue, unit, LengthUnitEnum.Cm);
    }

    protected static MeasurementEditorModel Length(
        decimal? canonicalValue = null,
        LengthUnitEnum unit = LengthUnitEnum.Cm)
    {
        return new MeasurementEditorModel(false, canonicalValue, WeightUnitEnum.Kg, unit);
    }
}
