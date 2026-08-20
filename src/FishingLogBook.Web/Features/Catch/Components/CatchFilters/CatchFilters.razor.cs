using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Features.Catch.Components.CatchFilters;

public partial class CatchFilters : ComponentBase
{
    [Parameter, EditorRequired]
    public CatchFilterModel Filters { get; set; } = new();

    [Parameter]
    public EventCallback<CatchFilterModel> FiltersChanged { get; set; }

    [Parameter]
    public IReadOnlyList<string> MethodOptions { get; set; } = [];

    [Parameter]
    public IReadOnlyList<string> SpeciesOptions { get; set; } = [];

    [Inject]
    private IStringLocalizer<UiStrings> Loc { get; set; } = default!;

    private static readonly IReadOnlyList<CatchDateRangeFilter> DateRangeOptions =
    [
        CatchDateRangeFilter.Today,
        CatchDateRangeFilter.Last7Days,
        CatchDateRangeFilter.Last30Days
    ];

    private Task OnSearchChanged(string value)
    {
        return FiltersChanged.InvokeAsync(Filters with { SearchTerm = value });
    }

    private Task SelectMethod(string? method)
    {
        var next = string.Equals(Filters.Method, method, StringComparison.OrdinalIgnoreCase) ? null : method;
        return FiltersChanged.InvokeAsync(Filters with { Method = next });
    }

    private Task SelectSpecies(string? species)
    {
        var next = string.Equals(Filters.Species, species, StringComparison.OrdinalIgnoreCase) ? null : species;
        return FiltersChanged.InvokeAsync(Filters with { Species = next });
    }

    private Task SelectDateRange(CatchDateRangeFilter dateRange)
    {
        var next = Filters.DateRange == dateRange ? CatchDateRangeFilter.All : dateRange;
        return FiltersChanged.InvokeAsync(Filters with { DateRange = next });
    }

    private Task ClearSearch()
    {
        return FiltersChanged.InvokeAsync(Filters with { SearchTerm = string.Empty });
    }

    private Task ClearMethod()
    {
        return FiltersChanged.InvokeAsync(Filters with { Method = null });
    }

    private Task ClearSpecies()
    {
        return FiltersChanged.InvokeAsync(Filters with { Species = null });
    }

    private Task ClearDateRange()
    {
        return FiltersChanged.InvokeAsync(Filters with { DateRange = CatchDateRangeFilter.All });
    }

    private Task ClearAll()
    {
        return FiltersChanged.InvokeAsync(new CatchFilterModel());
    }

    private string DateRangeLabel(CatchDateRangeFilter dateRange)
    {
        return dateRange switch
        {
            CatchDateRangeFilter.Today => Loc["Catch_DateRange_Today"],
            CatchDateRangeFilter.Last7Days => Loc["Catch_DateRange_Last7Days"],
            CatchDateRangeFilter.Last30Days => Loc["Catch_DateRange_Last30Days"],
            _ => Loc["Catch_FilterAll"]
        };
    }
}
