using System.Globalization;
using FishingLogBook.Web.Browser.Time;
using NSubstitute;

namespace FishingLogBook.Web.Tests.TestSupport;

internal static class TestTimeService
{
    public static ITimeService WithOffset(TimeSpan offset)
    {
        var time = Substitute.For<ITimeService>();
        time.ToDateTimeLocalValueAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(call => ToDateTimeLocal(call.Arg<DateTimeOffset>(), offset));
        time.FromDateTimeLocalValueAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => FromDateTimeLocal(call.Arg<string>(), offset));
        return time;
    }

    public static string ToDateTimeLocal(DateTimeOffset instant, TimeSpan offset)
    {
        return instant.ToUniversalTime().UtcDateTime.Add(offset)
            .ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);
    }

    private static DateTimeOffset? FromDateTimeLocal(string localValue, TimeSpan offset)
    {
        if (!DateTime.TryParse(
                localValue,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            return null;
        }

        var utc = DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified).Subtract(offset);
        return new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc));
    }
}
