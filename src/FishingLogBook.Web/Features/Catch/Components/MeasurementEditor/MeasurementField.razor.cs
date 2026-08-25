using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace FishingLogBook.Web.Features.Catch.Components.MeasurementEditor;

public partial class MeasurementField : ComponentBase
{
    [Parameter]
    public bool IsWeight { get; set; }

    [Parameter]
    public decimal? Value { get; set; }

    [Parameter]
    public EventCallback<decimal?> ValueChanged { get; set; }

    [Parameter]
    public WeightUnitEnum WeightUnit { get; set; } = WeightUnitEnum.Kg;

    [Parameter]
    public LengthUnitEnum LengthUnit { get; set; } = LengthUnitEnum.Cm;

    [Parameter, EditorRequired]
    public string Id { get; set; } = string.Empty;

    [Inject]
    private IMeasurementService Measurement { get; set; } = default!;

    [Inject]
    private IModalService ModalService { get; set; } = default!;

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private string Label => IsWeight ? Loc["Catch_EditWeight"] : Loc["Catch_EditLength"];

    private string Icon => IsWeight ? Icons.Material.Filled.Scale : Icons.Material.Filled.Straighten;

    private string DisplayValue
    {
        get
        {
            if (Value is null)
            {
                return Loc["Catch_MeasurementNotRecorded"];
            }

            return IsWeight
                ? Measurement.FormatWeight(Value, WeightUnit, WeightUnitLabel, Loc["Catch_WeightUnitShort_Oz"])
                : Measurement.FormatLength(Value, LengthUnit, LengthUnitLabel);
        }
    }

    private string AccessibleLabel => Loc["Catch_MeasurementOpen", Label, DisplayValue];

    private string WeightUnitLabel => WeightUnit == WeightUnitEnum.Lb
        ? Loc["Catch_WeightUnitShort_Lb"]
        : Loc["Catch_WeightUnitShort_Kg"];

    private string LengthUnitLabel => LengthUnit == LengthUnitEnum.In
        ? Loc["Catch_LengthUnitShort_In"]
        : Loc["Catch_LengthUnitShort_Cm"];

    private async Task OpenAsync()
    {
        var result = await ModalService
            .ShowAsync<MeasurementEditorModal, MeasurementEditorModel, MeasurementEditorResult>(
                new MeasurementEditorModel(IsWeight, Value, WeightUnit, LengthUnit),
                CancellationToken.None);
        if (result is not null)
        {
            await ValueChanged.InvokeAsync(result.CanonicalValue);
        }
    }
}
