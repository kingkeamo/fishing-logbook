namespace FishingLogBook.Shared.Dtos;

public sealed record DashboardPersonalBestDto
{
    public string? SpeciesName { get; init; }
    public string? Method { get; init; }
    public decimal Weight { get; init; }
    public Guid CatchId { get; init; }
}
