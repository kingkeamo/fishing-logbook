using FishingLogBook.Web.Features.Import.Enums;

namespace FishingLogBook.Web.Features.Import.Models;

public sealed record ImportTimestampModel
{
    private ImportTimestampModel(
        ImportTimestampStateEnum state,
        ImportTimestampSourceEnum source,
        DateTimeOffset? instant,
        DateTime? localWallClock)
    {
        State = state;
        Source = source;
        Instant = instant;
        LocalWallClock = localWallClock;
    }

    public ImportTimestampStateEnum State { get; }

    public ImportTimestampSourceEnum Source { get; }

    public DateTimeOffset? Instant { get; }

    public DateTime? LocalWallClock { get; }

    public bool HasTimezoneAmbiguity
    {
        get
        {
            return State == ImportTimestampStateEnum.LocalWallClock;
        }
    }

    public bool IsResolved
    {
        get
        {
            return State is ImportTimestampStateEnum.ExplicitInstant
                or ImportTimestampStateEnum.UserConfirmed;
        }
    }

    public bool RequiresUtcOffset => LocalWallClock.HasValue && !Instant.HasValue;

    public static ImportTimestampModel Missing()
    {
        return new ImportTimestampModel(
            ImportTimestampStateEnum.Missing,
            ImportTimestampSourceEnum.None,
            null,
            null);
    }

    public static ImportTimestampModel Unusable(ImportTimestampSourceEnum source)
    {
        return new ImportTimestampModel(ImportTimestampStateEnum.Unusable, source, null, null);
    }

    public static ImportTimestampModel FromExplicitInstant(
        DateTimeOffset instant,
        ImportTimestampSourceEnum source)
    {
        RequireExifSource(source);
        return new ImportTimestampModel(ImportTimestampStateEnum.ExplicitInstant, source, instant, null);
    }

    public static ImportTimestampModel FromLocalWallClock(
        DateTime localWallClock,
        ImportTimestampSourceEnum source)
    {
        RequireExifSource(source);
        return new ImportTimestampModel(
            ImportTimestampStateEnum.LocalWallClock,
            source,
            null,
            DateTime.SpecifyKind(localWallClock, DateTimeKind.Unspecified));
    }

    public static ImportTimestampModel FromWeakFallback(DateTimeOffset instant)
    {
        return new ImportTimestampModel(
            ImportTimestampStateEnum.WeakFallback,
            ImportTimestampSourceEnum.FileLastModified,
            instant,
            null);
    }

    public static ImportTimestampModel UserConfirmed(DateTimeOffset instant)
    {
        return new ImportTimestampModel(
            ImportTimestampStateEnum.UserConfirmed,
            ImportTimestampSourceEnum.User,
            instant,
            null);
    }

    public ImportTimestampModel Confirm(DateTime localValue)
    {
        if (!Instant.HasValue)
        {
            throw new InvalidOperationException("An explicit UTC offset is required for a historical local date and time.");
        }

        var unspecified = DateTime.SpecifyKind(localValue, DateTimeKind.Unspecified);
        return UserConfirmed(new DateTimeOffset(unspecified, Instant.Value.Offset));
    }

    public ImportTimestampModel ConfirmLocalWallClock(DateTime localWallClock, TimeSpan utcOffset)
    {
        var unspecified = DateTime.SpecifyKind(localWallClock, DateTimeKind.Unspecified);
        var instant = new DateTimeOffset(unspecified, utcOffset);
        return new ImportTimestampModel(
            ImportTimestampStateEnum.UserConfirmed,
            ImportTimestampSourceEnum.User,
            instant,
            unspecified);
    }

    public ImportTimestampModel EditLocalWallClock(DateTime localWallClock)
    {
        return new ImportTimestampModel(
            ImportTimestampStateEnum.LocalWallClock,
            Source,
            null,
            DateTime.SpecifyKind(localWallClock, DateTimeKind.Unspecified));
    }

    private static void RequireExifSource(ImportTimestampSourceEnum source)
    {
        if (source is not ImportTimestampSourceEnum.ExifOriginal
            and not ImportTimestampSourceEnum.ExifDigitized)
        {
            throw new ArgumentException("An EXIF timestamp requires an EXIF source.", nameof(source));
        }
    }
}
