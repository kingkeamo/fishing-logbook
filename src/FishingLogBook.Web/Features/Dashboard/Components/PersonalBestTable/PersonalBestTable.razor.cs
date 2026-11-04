using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Shared.Measurements;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Dashboard.Components.PersonalBestTable;

public partial class PersonalBestTable : ComponentBase
{
    [Inject] private IStringLocalizer<UiStrings> Loc { get; set; } = default!;
    [Inject] private IMeasurementService Measurement { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Parameter] public string Id { get; set; } = string.Empty;
    [Parameter] public IReadOnlyList<DashboardPersonalBestDto> Rows { get; set; } = [];
    [Parameter] public WeightUnitEnum WeightUnit { get; set; }
    [Parameter] public bool IsLoaded { get; set; }
    [Parameter] public string EmptyText { get; set; } = string.Empty;

    private string Species(DashboardPersonalBestDto row)
    {
        return row.SpeciesName ?? Loc["DashboardUnknownSpecies"];
    }

    private void OpenCatch(Guid catchId)
    {
        Navigation.NavigateTo($"/catches/{catchId}");
    }

    private string Method(DashboardPersonalBestDto row)
    {
        return row.Method ?? Loc["DashboardUnknownMethod"];
    }

    private string Weight(DashboardPersonalBestDto row)
    {
        return Measurement.FormatWeight(row.Weight, WeightUnit,
            Loc[WeightUnit == WeightUnitEnum.Lb ? "Catch_WeightUnitShort_Lb" : "Catch_WeightUnitShort_Kg"],
            Loc["Catch_WeightUnitShort_Oz"]);
    }
}
