namespace FishingLogBook.Shared.Photographs.Contracts;

public interface IPhotographLocalTimeMapper
{
    Task<DateTimeOffset?> FromDateTimeLocalValueAsync(string localValue, CancellationToken cancellationToken);
}
