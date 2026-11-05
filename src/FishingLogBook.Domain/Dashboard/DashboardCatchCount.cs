namespace FishingLogBook.Domain.Dashboard;

public sealed class DashboardCatchCount
{
    public string? SpeciesName { get; init; }
    public string? Method { get; init; }
    public long Count { get; init; }
}
