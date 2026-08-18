namespace FishingLogBook.Domain.Catalogue;

public sealed class FishingMethod
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public DateTimeOffset CreatedOn { get; init; }
}
