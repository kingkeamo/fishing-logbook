namespace FishingLogBook.Domain.Catches;

public sealed class CatchDetail
{
    public Catch Catch { get; init; } = default!;

    public string? AnglerName { get; init; }

    public string? RecordedByName { get; init; }
}
