namespace FishingLogBook.Domain.Catalogue;

public sealed class Species
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public DateTimeOffset CreatedOn { get; init; }
}
