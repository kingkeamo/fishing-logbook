using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Catch.Components.MeasurementEditor;

public partial class MeasurementRangeSelector : ComponentBase
{
    [Parameter, EditorRequired]
    public IReadOnlyList<MeasurementScaleRangeModel> Ranges { get; set; } = [];

    [Parameter, EditorRequired]
    public MeasurementScaleRangeModel Selected { get; set; } = default!;

    [Parameter]
    public decimal? CanonicalValue { get; set; }

    [Parameter]
    public EventCallback<MeasurementScaleRangeModel> SelectedChanged { get; set; }

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private IEnumerable<MeasurementScaleRangeModel> LargestFirst => Ranges.Reverse();

    private string Label(MeasurementScaleRangeModel range) => Loc[range.LabelKey];

    private static string ElementId(MeasurementScaleRangeModel range) =>
        $"measurement-range-{range.Range}";

    private Task SelectAsync(MeasurementScaleRangeModel range)
    {
        return range.CanDisplay(CanonicalValue)
            ? SelectedChanged.InvokeAsync(range)
            : Task.CompletedTask;
    }
}
