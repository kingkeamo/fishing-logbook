namespace FishingLogBook.Domain.SystemStatus;

public sealed class SystemHealth
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public DateTimeOffset CreatedOn { get; init; }
}
