namespace FishingLogBook.Shared.Dtos;

public sealed record DashboardTotalsDto
{
    public long Catches { get; init; }
    public long Trips { get; init; }
    public long Species { get; init; }
}
