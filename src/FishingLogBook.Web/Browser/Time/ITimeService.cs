namespace FishingLogBook.Web.Browser.Time;

public interface ITimeService
{
    Task<string> ToDateTimeLocalValueAsync(DateTimeOffset instant, CancellationToken cancellationToken);

    Task<DateTimeOffset?> FromDateTimeLocalValueAsync(string localValue, CancellationToken cancellationToken);
}
