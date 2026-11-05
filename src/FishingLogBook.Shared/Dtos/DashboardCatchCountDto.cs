namespace FishingLogBook.Shared.Dtos;

public sealed record DashboardCatchCountDto
{
    public string? SpeciesName { get; init; }
    public string? Method { get; init; }
    public long Count { get; init; }
}
