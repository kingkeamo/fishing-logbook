using System.Text;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Features.Photographs.Models;
using FishingLogBook.Web.Features.Photographs.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Photographs.Services.PhotographMetadataServiceTests;

public class BasePhotographMetadataServiceTest
{
    protected const ushort DateTimeOriginalTag = 0x9003;
    protected const ushort DateTimeDigitizedTag = 0x9004;
    protected const ushort OffsetTimeOriginalTag = 0x9011;
    protected const ushort OffsetTimeDigitizedTag = 0x9012;

    protected static readonly DateTimeOffset ReferenceNow =
        DateTimeOffset.Parse("2026-08-26T12:00:00Z");

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
        bytes.AddRange([0xFF, 0xDB, 0x00, 0x05, 0x00, 0x01, 0x02]);
        bytes.AddRange([0xFF, 0xDA, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x3F, 0x00]);
        bytes.AddRange([0x9A, 0x2B, 0x7C, 0x11, 0x4D, 0xE0]);
        bytes.AddRange([0xFF, 0xD9]);
        return [.. bytes];
    }

    protected static byte[] JpegWithExtraSegments(
        ExifContent content,
        params (byte Marker, byte[] Payload)[] extras)
    {
        var jpeg = Jpeg(content).ToList();
        var insertAt = ScanStart(jpeg);
        var extraBytes = new List<byte>();
        foreach (var (marker, payload) in extras)
        {
            extraBytes.AddRange([0xFF, marker]);
            extraBytes.AddRange(BigEndian((ushort)(payload.Length + 2)));
            extraBytes.AddRange(payload);
        }

        jpeg.InsertRange(insertAt, extraBytes);
        return [.. jpeg];
    }

    protected static byte[] BuildXmpPayload()
    {
        return Encoding.ASCII.GetBytes(
            "http://ns.adobe.com/xap/1.0/\0<x:xmpmeta xmlns:x=\"adobe:ns:meta/\"></x:xmpmeta>");
    }

    private static int ScanStart(List<byte> jpeg)
    {
        for (var index = 2; index + 1 < jpeg.Count; index++)
        {
            if (jpeg[index] == 0xFF && jpeg[index + 1] == 0xDA)
            {
                return index;
            }
        }

        return jpeg.Count - 2;
    }

    protected static byte[] ScanPayload(byte[] jpeg)
    {
        for (var index = 2; index + 1 < jpeg.Length; index++)
        {
            if (jpeg[index] == 0xFF && jpeg[index + 1] == 0xDA)
            {
                return jpeg[index..];
            }
        }

        return [];
    }

    protected static ushort? ReadOrientationTag(byte[] bytes, string contentType)
    {
        var tiff = FindTiffStart(bytes, contentType);
        if (tiff < 0 || tiff + 8 > bytes.Length)
        {
            return null;
        }

        var bigEndian = bytes[tiff] == 0x4D;
        var ifd = tiff + (int)ReadUInt32At(bytes, tiff + 4, bigEndian);
        if (ifd + 2 > bytes.Length)
        {
            return null;
        }

        var count = ReadUInt16At(bytes, ifd, bigEndian);
        for (var index = 0; index < count; index++)
        {
            var entry = ifd + 2 + (index * 12);
            if (entry + 12 > bytes.Length)
            {
                return null;
            }

            if (ReadUInt16At(bytes, entry, bigEndian) == 0x0112)
            {
                return ReadUInt16At(bytes, entry + 8, bigEndian);
            }
        }

        return null;
    }

    private static int FindTiffStart(byte[] bytes, string contentType)
    {
        var marker = contentType switch
        {
            "image/png" => "eXIf",
            "image/webp" => "EXIF",
            _ => "Exif"
        };
        var text = Encoding.Latin1.GetString(bytes);
        var at = text.IndexOf(marker, StringComparison.Ordinal);
        if (at < 0)
        {
            return -1;
        }

        return marker switch
        {
            "Exif" => at + 6,
            "eXIf" => at + 4,
            _ => at + 8
        };
    }

    private static ushort ReadUInt16At(byte[] bytes, int start, bool bigEndian)
    {
        return bigEndian
            ? (ushort)((bytes[start] << 8) | bytes[start + 1])
            : (ushort)((bytes[start + 1] << 8) | bytes[start]);
    }

    private static uint ReadUInt32At(byte[] bytes, int start, bool bigEndian)
    {
        return bigEndian
            ? ((uint)bytes[start] << 24) | ((uint)bytes[start + 1] << 16) | ((uint)bytes[start + 2] << 8) | bytes[start + 3]
            : ((uint)bytes[start + 3] << 24) | ((uint)bytes[start + 2] << 16) | ((uint)bytes[start + 1] << 8) | bytes[start];
    }

    protected static uint ReadBigEndianUInt32(byte[] bytes, int start)
    {
        return ReadUInt32At(bytes, start, bigEndian: true);
    }

    protected static uint ReadLittleEndianUInt32(byte[] bytes, int start)
    {
        return ReadUInt32At(bytes, start, bigEndian: false);
    }

    protected static uint Crc32(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? 0xEDB88320 ^ (crc >> 1) : crc >> 1;
            }
        }

        return crc ^ 0xFFFFFFFFu;
    }

    protected static byte[] JpegWithoutExif()
    {
        return [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x04, 0x00, 0x00, 0xFF, 0xD9];
    }

    protected static byte[] PngWithoutMetadata()
    {
        var bytes = new List<byte> { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        AppendPngChunk(bytes, "IHDR", [0, 0, 0, 1, 0, 0, 0, 1, 8, 6, 0, 0, 0]);
        AppendPngChunk(bytes, "IDAT", [0x78, 0x9C, 0x63, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01]);
        AppendPngChunk(bytes, "IEND", []);
        return [.. bytes];
    }

    protected static byte[] Png(ExifContent content)
    {
        var bytes = new List<byte> { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        AppendPngChunk(bytes, "IHDR", [0, 0, 0, 1, 0, 0, 0, 1, 8, 6, 0, 0, 0]);
        AppendPngChunk(bytes, "eXIf", Tiff(content, bigEndian: false));
        AppendPngChunk(bytes, "tEXt", Encoding.ASCII.GetBytes("Comment\0shot at 53.2707,-9.0568"));
        AppendPngChunk(bytes, "IDAT", [0x78, 0x9C, 0x63, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01]);
        AppendPngChunk(bytes, "IEND", []);
        return [.. bytes];
    }

    protected static void AppendPngChunk(List<byte> bytes, string type, byte[] data)
    {
        var body = new List<byte>(Encoding.ASCII.GetBytes(type));
        body.AddRange(data);
        bytes.AddRange(BigEndian((uint)data.Length));
        bytes.AddRange(body);
        bytes.AddRange(BigEndian(Crc32([.. body])));
    }

    protected static IReadOnlyList<string> PngChunkTypes(byte[] bytes)
    {
        var types = new List<string>();
        var position = 8;
        while (position + 12 <= bytes.Length)
        {
            var length = (int)ReadBigEndianUInt32(bytes, position);
            types.Add(Encoding.ASCII.GetString(bytes, position + 4, 4));
            position += 12 + length;
        }

        return types;
    }

    protected static bool PngChunksAreWellFormed(byte[] bytes)
    {
        var position = 8;
        while (position + 12 <= bytes.Length)
        {
            var length = (int)ReadBigEndianUInt32(bytes, position);
            if (position + 12 + length > bytes.Length)
            {
                return false;
            }

            var body = bytes[(position + 4)..(position + 8 + length)];
            if (Crc32(body) != ReadBigEndianUInt32(bytes, position + 8 + length))
            {
                return false;
            }

            position += 12 + length;
        }

        return position == bytes.Length;
    }

    protected static byte[] Webp(ExifContent content, bool withExtendedHeader = false)
    {
        var tiff = Tiff(content, bigEndian: false);
        var chunks = new List<byte>();
        chunks.AddRange("WEBP"u8.ToArray());
        if (withExtendedHeader)
        {
            AppendWebpChunk(chunks, "VP8X", [0x0C, 0, 0, 0, 0, 0, 0, 0, 0, 0]);
        }

        AppendWebpChunk(chunks, "VP8 ", new byte[4]);
        AppendWebpChunk(chunks, "EXIF", tiff);
        var bytes = new List<byte>();
        bytes.AddRange("RIFF"u8.ToArray());
        bytes.AddRange(LittleEndian((uint)chunks.Count));
        bytes.AddRange(chunks);
        return [.. bytes];
    }

    protected static void AppendWebpChunk(List<byte> chunks, string type, byte[] data)
    {
        chunks.AddRange(Encoding.ASCII.GetBytes(type));
        chunks.AddRange(LittleEndian((uint)data.Length));
        chunks.AddRange(data);
        if (data.Length % 2 == 1)
        {
            chunks.Add(0);
        }
    }

    protected static IReadOnlyList<string> WebpChunkTypes(byte[] bytes)
    {
        var types = new List<string>();
        var position = 12;
        while (position + 8 <= bytes.Length)
        {
            var size = (int)ReadLittleEndianUInt32(bytes, position + 4);
            types.Add(Encoding.ASCII.GetString(bytes, position, 4));
            position += 8 + size + (size & 1);
        }

        return types;
    }

    protected static bool WebpRiffSizeMatches(byte[] bytes)
    {
        return ReadLittleEndianUInt32(bytes, 4) == (uint)(bytes.Length - 8);
    }

    protected static byte WebpExtendedFlags(byte[] bytes)
    {
        var position = 12;
        while (position + 8 <= bytes.Length)
        {
            var size = (int)ReadLittleEndianUInt32(bytes, position + 4);
            if (Encoding.ASCII.GetString(bytes, position, 4) == "VP8X")
            {
                return bytes[position + 8];
            }

            position += 8 + size + (size & 1);
        }

        return 0;
    }

    protected static byte[] Tiff(ExifContent content, bool bigEndian)
    {
        var exifEntries = content.ExifText
            .Select(entry => AsciiEntry(entry.Key, entry.Value))
            .ToList();
        var gpsEntries = BuildGpsEntries(content, bigEndian);
        var rootEntries = new List<PendingEntry>();
        var rootSize = IfdSize(
            RootEntryCount(exifEntries.Count > 0, gpsEntries.Count > 0) + (content.Orientation is null ? 0 : 1));
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

        if (content.Orientation is not null)
        {
            rootEntries.Add(new PendingEntry(0x0112, 3, 1, ShortValue(content.Orientation.Value, bigEndian)));
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

    private static byte[] ShortValue(ushort value, bool bigEndian)
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

    protected static byte[] BigEndian(ushort value)
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

        public ushort? Orientation { get; init; }
    }

    private sealed record PendingEntry(ushort Tag, ushort Type, uint Count, byte[] Value);
}
