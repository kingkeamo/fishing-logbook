namespace FishingLogBook.Domain.Dashboard;

public sealed class DashboardPersonalBest
{
    public string? SpeciesName { get; init; }
    public string? Method { get; init; }
    public decimal Weight { get; init; }
    public Guid CatchId { get; init; }
}
