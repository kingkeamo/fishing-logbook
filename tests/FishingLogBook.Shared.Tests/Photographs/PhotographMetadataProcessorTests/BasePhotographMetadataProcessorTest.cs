using FishingLogBook.Shared.Photographs.Contracts;
using FishingLogBook.Shared.Photographs.Services;
using FishingLogBook.Tests.Common.Photographs;
using NSubstitute;

namespace FishingLogBook.Shared.Tests.Photographs.PhotographMetadataProcessorTests;

public class BasePhotographMetadataProcessorTest : PhotographImageTestBase
{
    protected static readonly DateTimeOffset ReferenceNow =
        DateTimeOffset.Parse("2026-08-26T12:00:00Z");

    protected static PhotographMetadataProcessor CreateProcessor(IPhotographLocalTimeMapper time)
    {
        return new PhotographMetadataProcessor(time);
    }

    protected static IPhotographLocalTimeMapper LocalTime(TimeSpan offset)
    {
        var time = Substitute.For<IPhotographLocalTimeMapper>();
        time.FromDateTimeLocalValueAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(ToInstant(call.ArgAt<string>(0), offset)));
        return time;
    }

    protected static IPhotographLocalTimeMapper UnavailableLocalTime()
    {
        var time = Substitute.For<IPhotographLocalTimeMapper>();
        time.FromDateTimeLocalValueAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((DateTimeOffset?)null);
        return time;
    }

    private static DateTimeOffset? ToInstant(string localValue, TimeSpan offset)
    {
        return DateTime.TryParse(localValue, out var parsed)
            ? new DateTimeOffset(parsed, offset).ToUniversalTime()
            : null;
    }
}
