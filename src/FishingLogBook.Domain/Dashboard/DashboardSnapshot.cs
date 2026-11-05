namespace FishingLogBook.Domain.Dashboard;

public sealed class DashboardSnapshot
{
    public DashboardTotals Lifetime { get; init; } = new();
    public DashboardTotals Filtered { get; init; } = new();
    public IReadOnlyList<DashboardPersonalBest> AllTimePersonalBests { get; init; } = [];
    public IReadOnlyList<DashboardCatchCount> CatchStatistics { get; init; } = [];
    public IReadOnlyList<DashboardPersonalBest> FilteredPersonalBests { get; init; } = [];
    public IReadOnlyList<string> Species { get; init; } = [];
    public IReadOnlyList<string> Methods { get; init; } = [];
    public IReadOnlyList<string> Venues { get; init; } = [];
}
