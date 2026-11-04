namespace FishingLogBook.Shared.Dtos;

public sealed record DashboardFilterDto
{
    public DateTimeOffset? FromInclusive { get; init; }
    public DateTimeOffset? ToExclusive { get; init; }
    public string? Species { get; init; }
    public string? Method { get; init; }
    public string? Venue { get; init; }
}
