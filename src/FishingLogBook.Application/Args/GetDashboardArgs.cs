namespace FishingLogBook.Application.Args;

public sealed class GetDashboardArgs
{
    public Guid UserId { get; init; }
    public DateTimeOffset? FromInclusive { get; init; }
    public DateTimeOffset? ToExclusive { get; init; }
    public string? Species { get; init; }
    public string? Method { get; init; }
    public string? Venue { get; init; }
}
