namespace FishingLogBook.Web.Features.Catch.Models;

public sealed record CatchFilterModel(
    string SearchTerm = "",
    string? Method = null,
    string? Species = null,
    CatchDateRangeFilter DateRange = CatchDateRangeFilter.All)
{
    public bool HasActiveFilters
    {
        get
        {
            return !string.IsNullOrWhiteSpace(SearchTerm)
                || Method is not null
                || Species is not null
                || DateRange != CatchDateRangeFilter.All;
        }
    }
}
