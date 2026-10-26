namespace FishingLogBook.Mobile.Features.Catch.Models;

public sealed record CatchFilterModel(
    CatchMethodEnum Method,
    IReadOnlySet<CatchSpeciesEnum> Species,
    CatchDateRangeEnum DateRange,
    string Location)
{
    public static CatchFilterModel None { get; } = new(CatchMethodEnum.All, new HashSet<CatchSpeciesEnum>(), CatchDateRangeEnum.AllTime, string.Empty);

    public int ActiveCount
    {
        get
        {
            var method = Method == CatchMethodEnum.All ? 0 : 1;
            var range = DateRange == CatchDateRangeEnum.AllTime ? 0 : 1;
            var location = string.IsNullOrWhiteSpace(Location) ? 0 : 1;
            return method + Species.Count + range + location;
        }
    }
}
