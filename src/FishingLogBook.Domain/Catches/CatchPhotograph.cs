namespace FishingLogBook.Domain.Catches;

public sealed class CatchPhotograph
{
    public Guid Id { get; init; }

    public Guid CatchId { get; init; }

    public string ContentType { get; init; } = string.Empty;
}
