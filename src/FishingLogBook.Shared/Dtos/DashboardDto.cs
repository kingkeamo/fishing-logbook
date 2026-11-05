namespace FishingLogBook.Shared.Dtos;

public sealed record DashboardDto
{
    public DashboardTotalsDto Lifetime { get; init; } = new();
    public DashboardTotalsDto Filtered { get; init; } = new();
    public IReadOnlyList<DashboardPersonalBestDto> AllTimePersonalBests { get; init; } = [];
    public IReadOnlyList<DashboardCatchCountDto> CatchStatistics { get; init; } = [];
    public IReadOnlyList<DashboardPersonalBestDto> FilteredPersonalBests { get; init; } = [];
    public IReadOnlyList<string> Species { get; init; } = [];
    public IReadOnlyList<string> Methods { get; init; } = [];
    public IReadOnlyList<string> Venues { get; init; } = [];
}
