using System.Text;
using FishingLogBook.Web.Browser.Time;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Services.PhotoMetadataServiceTests;

public class BasePhotoMetadataServiceTest
{
    protected const ushort DateTimeOriginalTag = 0x9003;
    protected const ushort DateTimeDigitizedTag = 0x9004;
    protected const ushort OffsetTimeOriginalTag = 0x9011;
    protected const ushort OffsetTimeDigitizedTag = 0x9012;

    protected static ITimeService BrowserTime(TimeSpan offset)
    {
        var time = Substitute.For<ITimeService>();
        time.FromDateTimeLocalValueAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(ToInstant(call.ArgAt<string>(0), offset)));
        return time;
    }

    protected static ITimeService UnavailableBrowserTime()
    {
        var time = Substitute.For<ITimeService>();
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

    protected static byte[] Jpeg(ExifContent content, bool bigEndian = false, bool withJfifSegment = true)
    {
        var tiff = Tiff(content, bigEndian);
        var payload = new List<byte>();
        payload.AddRange("Exif\0\0"u8.ToArray());
        payload.AddRange(tiff);
        var bytes = new List<byte> { 0xFF, 0xD8 };
        if (withJfifSegment)
        {
            bytes.AddRange([0xFF, 0xE0, 0x00, 0x10]);
            bytes.AddRange("JFIF\0"u8.ToArray());
            bytes.AddRange(new byte[9]);
        }

        bytes.AddRange([0xFF, 0xE1]);
        bytes.AddRange(BigEndian((ushort)(payload.Count + 2)));
        bytes.AddRange(payload);
        bytes.AddRange([0xFF, 0xD9]);
        return [.. bytes];
    }

    protected static byte[] JpegWithoutExif()
    {
        return [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x04, 0x00, 0x00, 0xFF, 0xD9];
    }

    protected static byte[] Png(ExifContent content)
    {
        var tiff = Tiff(content, bigEndian: false);
        var bytes = new List<byte> { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        bytes.AddRange(BigEndian((uint)tiff.Length));
        bytes.AddRange("eXIf"u8.ToArray());
        bytes.AddRange(tiff);
        bytes.AddRange(new byte[4]);
        return [.. bytes];
    }

    protected static byte[] Webp(ExifContent content)
    {
        var tiff = Tiff(content, bigEndian: false);
        var chunks = new List<byte>();
        chunks.AddRange("WEBP"u8.ToArray());
        chunks.AddRange("VP8 "u8.ToArray());
        chunks.AddRange(LittleEndian(4u));
        chunks.AddRange(new byte[4]);
        chunks.AddRange("EXIF"u8.ToArray());
        chunks.AddRange(LittleEndian((uint)tiff.Length));
        chunks.AddRange(tiff);
        if (tiff.Length % 2 == 1)
        {
            chunks.Add(0);
        }

        var bytes = new List<byte>();
        bytes.AddRange("RIFF"u8.ToArray());
        bytes.AddRange(LittleEndian((uint)chunks.Count));
        bytes.AddRange(chunks);
        return [.. bytes];
    }

    protected static byte[] Tiff(ExifContent content, bool bigEndian)
    {
        var exifEntries = content.ExifText
            .Select(entry => AsciiEntry(entry.Key, entry.Value))
            .ToList();
        var gpsEntries = BuildGpsEntries(content, bigEndian);
        var rootEntries = new List<PendingEntry>();
        var rootSize = IfdSize(RootEntryCount(exifEntries.Count > 0, gpsEntries.Count > 0));
        var exifOffset = 8 + rootSize;
        var gpsOffset = exifOffset + (exifEntries.Count > 0 ? IfdSize(exifEntries.Count) : 0);
        var dataOffset = gpsOffset + (gpsEntries.Count > 0 ? IfdSize(gpsEntries.Count) : 0);
        if (exifEntries.Count > 0)
        {
            rootEntries.Add(new PendingEntry(0x8769, 4, 1, LongValue((uint)exifOffset, bigEndian)));
        }

        if (gpsEntries.Count > 0)
        {
            rootEntries.Add(new PendingEntry(0x8825, 4, 1, LongValue((uint)gpsOffset, bigEndian)));
        }

        var data = new List<byte>();
        var bytes = new List<byte>();
        bytes.AddRange(bigEndian ? "MM"u8.ToArray() : "II"u8.ToArray());
        bytes.AddRange(Endian((ushort)42, bigEndian));
        bytes.AddRange(Endian(8u, bigEndian));
        bytes.AddRange(WriteIfd(rootEntries, bigEndian, dataOffset, data));
        if (exifEntries.Count > 0)
        {
            bytes.AddRange(WriteIfd(exifEntries, bigEndian, dataOffset, data));
        }

        if (gpsEntries.Count > 0)
        {
            bytes.AddRange(WriteIfd(gpsEntries, bigEndian, dataOffset, data));
        }

        bytes.AddRange(data);
        return [.. bytes];
    }

    private static int RootEntryCount(bool hasExif, bool hasGps)
    {
        return (hasExif ? 1 : 0) + (hasGps ? 1 : 0);
    }

    private static List<PendingEntry> BuildGpsEntries(ExifContent content, bool bigEndian)
    {
        var entries = new List<PendingEntry>();
        if (content.Latitude is null || content.Longitude is null)
        {
            return entries;
        }

        entries.Add(AsciiEntry(0x0001, content.LatitudeRef ?? (content.Latitude >= 0 ? "N" : "S")));
        entries.Add(RationalEntry(0x0002, Math.Abs(content.Latitude.Value), content.ZeroDenominator, bigEndian));
        entries.Add(AsciiEntry(0x0003, content.LongitudeRef ?? (content.Longitude >= 0 ? "E" : "W")));
        entries.Add(RationalEntry(0x0004, Math.Abs(content.Longitude.Value), content.ZeroDenominator, bigEndian));
        return entries;
    }

    private static int IfdSize(int entryCount)
    {
        return 2 + (entryCount * 12) + 4;
    }

    private static byte[] WriteIfd(
        List<PendingEntry> entries,
        bool bigEndian,
        int dataOffset,
        List<byte> data)
    {
        var bytes = new List<byte>();
        bytes.AddRange(Endian((ushort)entries.Count, bigEndian));
        foreach (var entry in entries)
        {
            bytes.AddRange(Endian(entry.Tag, bigEndian));
            bytes.AddRange(Endian(entry.Type, bigEndian));
            bytes.AddRange(Endian(entry.Count, bigEndian));
            if (entry.Value.Length <= 4)
            {
                var inline = new byte[4];
                entry.Value.CopyTo(inline, 0);
                bytes.AddRange(inline);
                continue;
            }

            bytes.AddRange(Endian((uint)(dataOffset + data.Count), bigEndian));
            data.AddRange(entry.Value);
        }

        bytes.AddRange(new byte[4]);
        return [.. bytes];
    }

    private static PendingEntry AsciiEntry(ushort tag, string value)
    {
        var bytes = new List<byte>(Encoding.ASCII.GetBytes(value)) { 0 };
        return new PendingEntry(tag, 2, (uint)bytes.Count, [.. bytes]);
    }

    private static PendingEntry RationalEntry(ushort tag, double degrees, bool zeroDenominator, bool bigEndian)
    {
        var whole = (uint)Math.Floor(degrees);
        var minutesTotal = (degrees - whole) * 60;
        var minutes = (uint)Math.Floor(minutesTotal);
        var seconds = (uint)Math.Round((minutesTotal - minutes) * 60 * 1000);
        var denominator = zeroDenominator ? 0u : 1u;
        var bytes = new List<byte>();
        bytes.AddRange(Endian(whole, bigEndian));
        bytes.AddRange(Endian(denominator, bigEndian));
        bytes.AddRange(Endian(minutes, bigEndian));
        bytes.AddRange(Endian(denominator, bigEndian));
        bytes.AddRange(Endian(seconds, bigEndian));
        bytes.AddRange(Endian(zeroDenominator ? 0u : 1000u, bigEndian));
        return new PendingEntry(tag, 5, 3, [.. bytes]);
    }

    private static byte[] LongValue(uint value, bool bigEndian)
    {
        return Endian(value, bigEndian);
    }

    private static byte[] Endian(ushort value, bool bigEndian)
    {
        return bigEndian ? BigEndian(value) : LittleEndian(value);
    }

    private static byte[] Endian(uint value, bool bigEndian)
    {
        return bigEndian ? BigEndian(value) : LittleEndian(value);
    }

    private static byte[] BigEndian(ushort value)
    {
        return [(byte)(value >> 8), (byte)value];
    }

    private static byte[] BigEndian(uint value)
    {
        return [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];
    }

    private static byte[] LittleEndian(ushort value)
    {
        return [(byte)value, (byte)(value >> 8)];
    }

    private static byte[] LittleEndian(uint value)
    {
        return [(byte)value, (byte)(value >> 8), (byte)(value >> 16), (byte)(value >> 24)];
    }

    protected sealed record ExifContent
    {
        public Dictionary<ushort, string> ExifText { get; init; } = [];

        public double? Latitude { get; init; }

        public double? Longitude { get; init; }

        public string? LatitudeRef { get; init; }

        public string? LongitudeRef { get; init; }

        public bool ZeroDenominator { get; init; }
    }

    private sealed record PendingEntry(ushort Tag, ushort Type, uint Count, byte[] Value);
}
