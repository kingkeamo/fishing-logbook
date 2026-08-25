using System.Globalization;
using System.Text;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Features.Catch.Enums;
using FishingLogBook.Web.Features.Catch.Models;

namespace FishingLogBook.Web.Features.Catch.Services;

public sealed class PhotoMetadataService : IPhotoMetadataService
{
    private const int EarliestPlausibleCaptureYear = 1900;
    private const int TiffHeaderLength = 8;
    private const int IfdEntryLength = 12;
    private const int ExifIdentifierLength = 6;
    private const int PngChunkOverhead = 12;
    private const ushort ExifIfdPointerTag = 0x8769;
    private const ushort GpsIfdPointerTag = 0x8825;
    private const ushort OrientationTag = 0x0112;
    private const ushort DateTimeOriginalTag = 0x9003;
    private const ushort DateTimeDigitizedTag = 0x9004;
    private const ushort OffsetTimeOriginalTag = 0x9011;
    private const ushort OffsetTimeDigitizedTag = 0x9012;
    private const ushort GpsLatitudeRefTag = 0x0001;
    private const ushort GpsLatitudeTag = 0x0002;
    private const ushort GpsLongitudeRefTag = 0x0003;
    private const ushort GpsLongitudeTag = 0x0004;
    private const ushort DefaultOrientation = 1;
    private const byte App0Marker = 0xE0;
    private const byte App1Marker = 0xE1;
    private const byte App2Marker = 0xE2;
    private const byte App14Marker = 0xEE;
    private const byte CommentMarker = 0xFE;
    private const byte StartOfScanMarker = 0xDA;
    private const byte EndOfImageMarker = 0xD9;
    private const byte WebpExifFlag = 0x08;
    private const byte WebpXmpFlag = 0x04;
    private const string ExifWallClockFormat = "yyyy:MM:dd HH:mm:ss";
    private const string ExifWallClockWithOffsetFormat = "yyyy:MM:dd HH:mm:sszzz";
    private const string DateTimeLocalFormat = "yyyy-MM-ddTHH:mm:ss";

    private static readonly byte[] ExifIdentifier = [0x45, 0x78, 0x69, 0x66, 0x00, 0x00];
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly uint[] CrcTable = BuildCrcTable();

    private static readonly HashSet<string> RetainedPngChunks =
    [
        "IHDR", "PLTE", "IDAT", "IEND", "tRNS", "gAMA", "cHRM", "sRGB",
        "iCCP", "bKGD", "pHYs", "sBIT", "hIST", "sPLT", "cICP",
        "acTL", "fcTL", "fdAT"
    ];

    private readonly ITimeService _time;

    public PhotoMetadataService(ITimeService time)
    {
        _time = time;
    }

    public async Task<PhotoMetadataModel> ReadAsync(
        byte[] bytes,
        string contentType,
        DateTimeOffset? fileLastModified,
        CancellationToken cancellationToken)
    {
        var tiffOffset = FindTiffOffset(bytes, contentType);
        var values = tiffOffset < 0 ? null : ReadExifValues(bytes, tiffOffset);
        var capturedOn = values is null
            ? null
            : await ResolveCapturedOnAsync(values, cancellationToken);
        if (capturedOn is not null)
        {
            return new PhotoMetadataModel(
                capturedOn,
                values!.Latitude,
                values.Longitude,
                values.UsedDigitized
                    ? PhotoCapturedOnSourceEnum.ExifDigitized
                    : PhotoCapturedOnSourceEnum.ExifOriginal);
        }

        var fallback = PlausibleFileTimestamp(fileLastModified);
        return new PhotoMetadataModel(
            fallback,
            values?.Latitude,
            values?.Longitude,
            fallback is null
                ? PhotoCapturedOnSourceEnum.None
                : PhotoCapturedOnSourceEnum.FileLastModified);
    }

    private static DateTimeOffset? PlausibleFileTimestamp(DateTimeOffset? fileLastModified)
    {
        if (fileLastModified is null
            || fileLastModified.Value == default
            || !IsPlausible(fileLastModified.Value.Year))
        {
            return null;
        }

        return fileLastModified.Value.ToUniversalTime();
    }

    public byte[]? Sanitise(byte[] bytes, string contentType)
    {
        var orientation = ReadOrientation(bytes, contentType);
        if (string.Equals(contentType, PhotographContentTypeConstants.Jpeg, StringComparison.OrdinalIgnoreCase))
        {
            return SanitiseJpeg(bytes, orientation);
        }

        if (string.Equals(contentType, PhotographContentTypeConstants.Png, StringComparison.OrdinalIgnoreCase))
        {
            return SanitisePng(bytes, orientation);
        }

        if (string.Equals(contentType, PhotographContentTypeConstants.Webp, StringComparison.OrdinalIgnoreCase))
        {
            return SanitiseWebp(bytes, orientation);
        }

        return null;
    }

    private static ushort? ReadOrientation(byte[] bytes, string contentType)
    {
        var tiffOffset = FindTiffOffset(bytes, contentType);
        if (tiffOffset < 0 || !TryReadTiffHeader(bytes, tiffOffset, out var bigEndian, out var firstIfdOffset))
        {
            return null;
        }

        var root = ReadIfd(bytes, tiffOffset, firstIfdOffset, bigEndian);
        if (!root.TryGetValue(OrientationTag, out var entry) || entry.Type != 3 || entry.Count != 1)
        {
            return null;
        }

        var orientation = ReadUInt16(bytes, entry.ValuePosition, bigEndian);
        return orientation is >= 1 and <= 8 ? orientation : null;
    }

    private static byte[]? SanitiseJpeg(byte[] bytes, ushort? orientation)
    {
        if (!TryReadJpegLayout(bytes, out var parts))
        {
            return null;
        }

        var retained = parts
            .Where(part => part.IsEntropy || IsRetainedJpegMarker(part.Marker))
            .ToArray();
        var output = new List<byte>(bytes.Length) { 0xFF, 0xD8 };
        var index = 0;
        if (retained.Length > 0 && !retained[0].IsEntropy && retained[0].Marker == App0Marker)
        {
            output.AddRange(bytes.AsSpan(retained[0].Start, retained[0].TotalLength));
            index = 1;
        }

        AppendOrientationApp1(output, orientation);
        for (; index < retained.Length; index++)
        {
            output.AddRange(bytes.AsSpan(retained[index].Start, retained[index].TotalLength));
        }

        return [.. output];
    }

    private static void AppendOrientationApp1(List<byte> output, ushort? orientation)
    {
        if (orientation is null or DefaultOrientation)
        {
            return;
        }

        var tiff = BuildOrientationTiff(orientation.Value);
        var payloadLength = ExifIdentifierLength + tiff.Length + 2;
        output.AddRange([0xFF, App1Marker, (byte)(payloadLength >> 8), (byte)payloadLength]);
        output.AddRange(ExifIdentifier);
        output.AddRange(tiff);
    }

    private static bool IsRetainedJpegMarker(byte marker)
    {
        if (marker == CommentMarker)
        {
            return false;
        }

        if (marker is < 0xE0 or > 0xEF)
        {
            return true;
        }

        return marker is App0Marker or App2Marker or App14Marker;
    }

    private static byte[]? SanitisePng(byte[] bytes, ushort? orientation)
    {
        if (!TryReadPngChunks(bytes, out var chunks))
        {
            return null;
        }

        var output = new List<byte>(bytes.Length);
        output.AddRange(PngSignature);
        foreach (var chunk in chunks)
        {
            if (!RetainedPngChunks.Contains(chunk.Type))
            {
                continue;
            }

            if (chunk.Type == "IEND")
            {
                AppendOrientationPngChunk(output, orientation);
            }

            output.AddRange(bytes.AsSpan(chunk.Start, chunk.TotalLength));
        }

        return [.. output];
    }

    private static void AppendOrientationPngChunk(List<byte> output, ushort? orientation)
    {
        if (orientation is null or DefaultOrientation)
        {
            return;
        }

        var tiff = BuildOrientationTiff(orientation.Value);
        var chunk = new List<byte>();
        chunk.AddRange("eXIf"u8.ToArray());
        chunk.AddRange(tiff);
        output.AddRange(BigEndianBytes((uint)tiff.Length));
        output.AddRange(chunk);
        output.AddRange(BigEndianBytes(Crc32([.. chunk])));
    }

    private static byte[]? SanitiseWebp(byte[] bytes, ushort? orientation)
    {
        if (!TryReadWebpChunks(bytes, out var chunks))
        {
            return null;
        }

        var keepOrientation = orientation is not (null or DefaultOrientation)
            && chunks.Any(chunk => chunk.Type == "VP8X");
        var payload = new List<byte>();
        payload.AddRange("WEBP"u8.ToArray());
        foreach (var chunk in chunks)
        {
            if (chunk.Type is "EXIF" or "XMP ")
            {
                continue;
            }

            var start = payload.Count;
            payload.AddRange(bytes.AsSpan(chunk.Start, chunk.TotalLength));
            if (chunk.Type == "VP8X" && chunk.DataLength > 0)
            {
                var flagsAt = start + 8;
                payload[flagsAt] = (byte)(payload[flagsAt] & ~WebpXmpFlag);
                payload[flagsAt] = keepOrientation
                    ? (byte)(payload[flagsAt] | WebpExifFlag)
                    : (byte)(payload[flagsAt] & ~WebpExifFlag);
            }
        }

        if (keepOrientation)
        {
            AppendWebpChunk(payload, "EXIF", BuildOrientationTiff(orientation!.Value));
        }

        var output = new List<byte>(payload.Count + 8);
        output.AddRange("RIFF"u8.ToArray());
        output.AddRange(LittleEndianBytes((uint)payload.Count));
        output.AddRange(payload);
        return [.. output];
    }

    private static void AppendWebpChunk(List<byte> payload, string type, byte[] data)
    {
        payload.AddRange(Encoding.ASCII.GetBytes(type));
        payload.AddRange(LittleEndianBytes((uint)data.Length));
        payload.AddRange(data);
        if (data.Length % 2 == 1)
        {
            payload.Add(0);
        }
    }

    private static byte[] BuildOrientationTiff(ushort orientation)
    {
        return
        [
            0x49, 0x49, 0x2A, 0x00,
            0x08, 0x00, 0x00, 0x00,
            0x01, 0x00,
            0x12, 0x01,
            0x03, 0x00,
            0x01, 0x00, 0x00, 0x00,
            (byte)orientation, (byte)(orientation >> 8), 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00
        ];
    }

    private async Task<DateTimeOffset?> ResolveCapturedOnAsync(
        ExifValues values,
        CancellationToken cancellationToken)
    {
        if (values.WallClockText is null)
        {
            return null;
        }

        if (values.OffsetText is not null
            && DateTimeOffset.TryParseExact(
                values.WallClockText + values.OffsetText,
                ExifWallClockWithOffsetFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var withOffset))
        {
            return IsPlausible(withOffset.Year) ? withOffset : null;
        }

        if (!DateTime.TryParseExact(
                values.WallClockText,
                ExifWallClockFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var wallClock)
            || !IsPlausible(wallClock.Year))
        {
            return null;
        }

        return await _time.FromDateTimeLocalValueAsync(
            wallClock.ToString(DateTimeLocalFormat, CultureInfo.InvariantCulture),
            cancellationToken);
    }

    private static bool IsPlausible(int year)
    {
        return year >= EarliestPlausibleCaptureYear;
    }

    private static int FindTiffOffset(byte[] bytes, string contentType)
    {
        if (string.Equals(contentType, PhotographContentTypeConstants.Jpeg, StringComparison.OrdinalIgnoreCase))
        {
            return FindJpegTiffOffset(bytes);
        }

        if (string.Equals(contentType, PhotographContentTypeConstants.Png, StringComparison.OrdinalIgnoreCase))
        {
            return FindPngTiffOffset(bytes);
        }

        if (string.Equals(contentType, PhotographContentTypeConstants.Webp, StringComparison.OrdinalIgnoreCase))
        {
            return FindWebpTiffOffset(bytes);
        }

        return -1;
    }

    private static int FindJpegTiffOffset(byte[] bytes)
    {
        if (!TryReadJpegLayout(bytes, out var parts))
        {
            return -1;
        }

        foreach (var part in parts)
        {
            if (!part.IsEntropy
                && part.Marker == App1Marker
                && StartsWithExifIdentifier(bytes, part.PayloadStart, part.PayloadLength))
            {
                return part.PayloadStart + ExifIdentifierLength;
            }
        }

        return -1;
    }

    private static int FindPngTiffOffset(byte[] bytes)
    {
        if (!TryReadPngChunks(bytes, out var chunks))
        {
            return -1;
        }

        foreach (var chunk in chunks)
        {
            if (chunk.Type == "eXIf")
            {
                return SkipOptionalExifIdentifier(bytes, chunk.DataStart, chunk.DataLength);
            }
        }

        return -1;
    }

    private static int FindWebpTiffOffset(byte[] bytes)
    {
        if (!TryReadWebpChunks(bytes, out var chunks))
        {
            return -1;
        }

        foreach (var chunk in chunks)
        {
            if (chunk.Type == "EXIF")
            {
                return SkipOptionalExifIdentifier(bytes, chunk.DataStart, chunk.DataLength);
            }
        }

        return -1;
    }

    private static bool TryReadJpegLayout(byte[] bytes, out List<JpegPart> parts)
    {
        parts = [];
        if (bytes.Length < 4 || bytes[0] != 0xFF || bytes[1] != 0xD8)
        {
            return false;
        }

        var position = 2;
        while (position + 2 <= bytes.Length)
        {
            if (bytes[position] != 0xFF)
            {
                return false;
            }

            var marker = bytes[position + 1];
            if (marker == 0xFF)
            {
                position++;
                continue;
            }

            if (marker == EndOfImageMarker)
            {
                parts.Add(JpegPart.Segment(marker, position, 2, position + 2, 0));
                return true;
            }

            if (IsStandaloneMarker(marker))
            {
                parts.Add(JpegPart.Segment(marker, position, 2, position + 2, 0));
                position += 2;
                continue;
            }

            if (position + 4 > bytes.Length)
            {
                return false;
            }

            var length = ReadUInt16(bytes, position + 2, bigEndian: true);
            if (length < 2 || position + 2 + length > bytes.Length)
            {
                return false;
            }

            parts.Add(JpegPart.Segment(marker, position, 2 + length, position + 4, length - 2));
            position += 2 + length;
            if (marker != StartOfScanMarker)
            {
                continue;
            }

            var entropyStart = position;
            position = FindNextJpegMarker(bytes, position);
            parts.Add(JpegPart.Entropy(entropyStart, position - entropyStart));
        }

        return false;
    }

    private static int FindNextJpegMarker(byte[] bytes, int position)
    {
        while (position + 1 < bytes.Length)
        {
            if (bytes[position] == 0xFF && !IsEntropyFollower(bytes[position + 1]))
            {
                return position;
            }

            position++;
        }

        return bytes.Length;
    }

    private static bool IsEntropyFollower(byte value)
    {
        return value == 0x00 || value == 0xFF || value is >= 0xD0 and <= 0xD7;
    }

    private static bool IsStandaloneMarker(byte marker)
    {
        return marker == 0x01 || marker == 0xD8 || marker is >= 0xD0 and <= 0xD7;
    }

    private static bool TryReadPngChunks(byte[] bytes, out List<PngChunk> chunks)
    {
        chunks = [];
        if (!StartsWith(bytes, PngSignature))
        {
            return false;
        }

        var position = PngSignature.Length;
        while (position + 8 <= bytes.Length)
        {
            var length = ReadUInt32(bytes, position, bigEndian: true);
            if (length > int.MaxValue - PngChunkOverhead
                || position + PngChunkOverhead + (int)length > bytes.Length)
            {
                return false;
            }

            var type = Encoding.ASCII.GetString(bytes, position + 4, 4);
            chunks.Add(new PngChunk(
                type,
                position,
                PngChunkOverhead + (int)length,
                position + 8,
                (int)length));
            position += PngChunkOverhead + (int)length;
        }

        return chunks.Count > 0;
    }

    private static bool TryReadWebpChunks(byte[] bytes, out List<WebpChunk> chunks)
    {
        chunks = [];
        if (bytes.Length < 16 || !IsChunkType(bytes, 0, "RIFF") || !IsChunkType(bytes, 8, "WEBP"))
        {
            return false;
        }

        var position = 12;
        while (position + 8 <= bytes.Length)
        {
            var size = ReadUInt32(bytes, position + 4, bigEndian: false);
            var padded = (int)size + ((int)size & 1);
            if (size > int.MaxValue - 9 || position + 8 + padded > bytes.Length)
            {
                return false;
            }

            chunks.Add(new WebpChunk(
                Encoding.ASCII.GetString(bytes, position, 4),
                position,
                8 + padded,
                position + 8,
                (int)size));
            position += 8 + padded;
        }

        return chunks.Count > 0;
    }

    private static int SkipOptionalExifIdentifier(byte[] bytes, int start, int length)
    {
        return StartsWithExifIdentifier(bytes, start, length)
            ? start + ExifIdentifierLength
            : start;
    }

    private static bool StartsWithExifIdentifier(byte[] bytes, int start, int length)
    {
        if (length < ExifIdentifierLength || start + ExifIdentifierLength > bytes.Length)
        {
            return false;
        }

        for (var index = 0; index < ExifIdentifierLength; index++)
        {
            if (bytes[start + index] != ExifIdentifier[index])
            {
                return false;
            }
        }

        return true;
    }

    private static ExifValues? ReadExifValues(byte[] bytes, int tiffOffset)
    {
        if (!TryReadTiffHeader(bytes, tiffOffset, out var bigEndian, out var firstIfdOffset))
        {
            return null;
        }

        var root = ReadIfd(bytes, tiffOffset, firstIfdOffset, bigEndian);
        var exif = ReadPointedIfd(bytes, tiffOffset, root, ExifIfdPointerTag, bigEndian);
        var gps = ReadPointedIfd(bytes, tiffOffset, root, GpsIfdPointerTag, bigEndian);
        var (wallClockText, offsetText, usedDigitized) = ReadCaptureText(bytes, tiffOffset, exif, bigEndian);
        var (latitude, longitude) = ReadCoordinates(bytes, tiffOffset, gps, bigEndian);
        return wallClockText is null && latitude is null
            ? null
            : new ExifValues(wallClockText, offsetText, usedDigitized, latitude, longitude);
    }

    private static bool TryReadTiffHeader(
        byte[] bytes,
        int tiffOffset,
        out bool bigEndian,
        out uint firstIfdOffset)
    {
        bigEndian = false;
        firstIfdOffset = 0;
        if (tiffOffset < 0 || tiffOffset + TiffHeaderLength > bytes.Length)
        {
            return false;
        }

        if (bytes[tiffOffset] == 0x4D && bytes[tiffOffset + 1] == 0x4D)
        {
            bigEndian = true;
        }
        else if (bytes[tiffOffset] != 0x49 || bytes[tiffOffset + 1] != 0x49)
        {
            return false;
        }

        if (ReadUInt16(bytes, tiffOffset + 2, bigEndian) != 42)
        {
            return false;
        }

        firstIfdOffset = ReadUInt32(bytes, tiffOffset + 4, bigEndian);
        return true;
    }

    private static Dictionary<ushort, IfdEntry> ReadIfd(
        byte[] bytes,
        int tiffOffset,
        uint ifdOffset,
        bool bigEndian)
    {
        var entries = new Dictionary<ushort, IfdEntry>();
        if (ifdOffset > int.MaxValue)
        {
            return entries;
        }

        var start = tiffOffset + (int)ifdOffset;
        if (start < tiffOffset || start + 2 > bytes.Length)
        {
            return entries;
        }

        var count = ReadUInt16(bytes, start, bigEndian);
        for (var index = 0; index < count; index++)
        {
            var position = start + 2 + (index * IfdEntryLength);
            if (position + IfdEntryLength > bytes.Length)
            {
                break;
            }

            var tag = ReadUInt16(bytes, position, bigEndian);
            entries[tag] = new IfdEntry(
                ReadUInt16(bytes, position + 2, bigEndian),
                ReadUInt32(bytes, position + 4, bigEndian),
                position + 8);
        }

        return entries;
    }

    private static Dictionary<ushort, IfdEntry> ReadPointedIfd(
        byte[] bytes,
        int tiffOffset,
        Dictionary<ushort, IfdEntry> root,
        ushort pointerTag,
        bool bigEndian)
    {
        if (!root.TryGetValue(pointerTag, out var pointer) || pointer.Count != 1)
        {
            return [];
        }

        return ReadIfd(bytes, tiffOffset, ReadUInt32(bytes, pointer.ValuePosition, bigEndian), bigEndian);
    }

    private static (string? WallClockText, string? OffsetText, bool UsedDigitized) ReadCaptureText(
        byte[] bytes,
        int tiffOffset,
        Dictionary<ushort, IfdEntry> exif,
        bool bigEndian)
    {
        var original = ReadAscii(bytes, tiffOffset, exif, DateTimeOriginalTag, bigEndian);
        if (original is not null)
        {
            return (original, ReadAscii(bytes, tiffOffset, exif, OffsetTimeOriginalTag, bigEndian), false);
        }

        var digitized = ReadAscii(bytes, tiffOffset, exif, DateTimeDigitizedTag, bigEndian);
        return digitized is null
            ? (null, null, false)
            : (digitized, ReadAscii(bytes, tiffOffset, exif, OffsetTimeDigitizedTag, bigEndian), true);
    }

    private static (double? Latitude, double? Longitude) ReadCoordinates(
        byte[] bytes,
        int tiffOffset,
        Dictionary<ushort, IfdEntry> gps,
        bool bigEndian)
    {
        var latitude = ReadCoordinate(
            bytes, tiffOffset, gps, GpsLatitudeTag, GpsLatitudeRefTag, 'N', 'S', bigEndian);
        var longitude = ReadCoordinate(
            bytes, tiffOffset, gps, GpsLongitudeTag, GpsLongitudeRefTag, 'E', 'W', bigEndian);
        if (latitude is null || longitude is null)
        {
            return (null, null);
        }

        if (Math.Abs(latitude.Value) > CatchLocationConstants.MaxLatitude
            || Math.Abs(longitude.Value) > CatchLocationConstants.MaxLongitude
            || (latitude.Value == 0 && longitude.Value == 0))
        {
            return (null, null);
        }

        return (latitude, longitude);
    }

    private static double? ReadCoordinate(
        byte[] bytes,
        int tiffOffset,
        Dictionary<ushort, IfdEntry> gps,
        ushort valueTag,
        ushort referenceTag,
        char positive,
        char negative,
        bool bigEndian)
    {
        var reference = ReadAscii(bytes, tiffOffset, gps, referenceTag, bigEndian)?.Trim().ToUpperInvariant();
        if (reference is null || reference.Length != 1)
        {
            return null;
        }

        var sign = reference[0] == positive ? 1 : reference[0] == negative ? -1 : 0;
        if (sign == 0)
        {
            return null;
        }

        var degrees = ReadDegrees(bytes, tiffOffset, gps, valueTag, bigEndian);
        return degrees is null ? null : sign * degrees.Value;
    }

    private static double? ReadDegrees(
        byte[] bytes,
        int tiffOffset,
        Dictionary<ushort, IfdEntry> gps,
        ushort tag,
        bool bigEndian)
    {
        if (!gps.TryGetValue(tag, out var entry) || entry.Type != 5 || entry.Count != 3)
        {
            return null;
        }

        var start = ResolveValuePosition(bytes, tiffOffset, entry, 24, bigEndian);
        if (start < 0)
        {
            return null;
        }

        var total = 0d;
        for (var index = 0; index < 3; index++)
        {
            var numerator = ReadUInt32(bytes, start + (index * 8), bigEndian);
            var denominator = ReadUInt32(bytes, start + (index * 8) + 4, bigEndian);
            if (denominator == 0)
            {
                return null;
            }

            total += numerator / (double)denominator / Math.Pow(60, index);
        }

        return double.IsFinite(total) ? total : null;
    }

    private static string? ReadAscii(
        byte[] bytes,
        int tiffOffset,
        Dictionary<ushort, IfdEntry> ifd,
        ushort tag,
        bool bigEndian)
    {
        if (!ifd.TryGetValue(tag, out var entry) || entry.Type != 2 || entry.Count is 0 or > 256)
        {
            return null;
        }

        var length = (int)entry.Count;
        var start = ResolveValuePosition(bytes, tiffOffset, entry, length, bigEndian);
        if (start < 0)
        {
            return null;
        }

        var text = Encoding.ASCII.GetString(bytes, start, length).TrimEnd('\0').Trim();
        return text.Length == 0 ? null : text;
    }

    private static int ResolveValuePosition(
        byte[] bytes,
        int tiffOffset,
        IfdEntry entry,
        int length,
        bool bigEndian)
    {
        if (length <= 4)
        {
            return entry.ValuePosition;
        }

        var offset = ReadUInt32(bytes, entry.ValuePosition, bigEndian);
        if (offset > int.MaxValue)
        {
            return -1;
        }

        var start = tiffOffset + (int)offset;
        return start < tiffOffset || start + length > bytes.Length ? -1 : start;
    }

    private static bool StartsWith(byte[] bytes, byte[] signature)
    {
        if (bytes.Length < signature.Length)
        {
            return false;
        }

        for (var index = 0; index < signature.Length; index++)
        {
            if (bytes[index] != signature[index])
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsChunkType(byte[] bytes, int start, string type)
    {
        if (start + type.Length > bytes.Length)
        {
            return false;
        }

        for (var index = 0; index < type.Length; index++)
        {
            if (bytes[start + index] != (byte)type[index])
            {
                return false;
            }
        }

        return true;
    }

    private static ushort ReadUInt16(byte[] bytes, int start, bool bigEndian)
    {
        if (start + 2 > bytes.Length)
        {
            return 0;
        }

        return bigEndian
            ? (ushort)((bytes[start] << 8) | bytes[start + 1])
            : (ushort)((bytes[start + 1] << 8) | bytes[start]);
    }

    private static uint ReadUInt32(byte[] bytes, int start, bool bigEndian)
    {
        if (start + 4 > bytes.Length)
        {
            return 0;
        }

        return bigEndian
            ? ((uint)bytes[start] << 24) | ((uint)bytes[start + 1] << 16)
                | ((uint)bytes[start + 2] << 8) | bytes[start + 3]
            : ((uint)bytes[start + 3] << 24) | ((uint)bytes[start + 2] << 16)
                | ((uint)bytes[start + 1] << 8) | bytes[start];
    }

    private static byte[] BigEndianBytes(uint value)
    {
        return [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];
    }

    private static byte[] LittleEndianBytes(uint value)
    {
        return [(byte)value, (byte)(value >> 8), (byte)(value >> 16), (byte)(value >> 24)];
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (var index = 0u; index < 256; index++)
        {
            var value = index;
            for (var bit = 0; bit < 8; bit++)
            {
                value = (value & 1) != 0 ? 0xEDB88320 ^ (value >> 1) : value >> 1;
            }

            table[index] = value;
        }

        return table;
    }

    private static uint Crc32(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in data)
        {
            crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFFu;
    }

    private readonly record struct IfdEntry(ushort Type, uint Count, int ValuePosition);

    private readonly record struct JpegPart(
        byte Marker,
        int Start,
        int TotalLength,
        int PayloadStart,
        int PayloadLength,
        bool IsEntropy)
    {
        public static JpegPart Segment(
            byte marker,
            int start,
            int totalLength,
            int payloadStart,
            int payloadLength)
        {
            return new JpegPart(marker, start, totalLength, payloadStart, payloadLength, false);
        }

        public static JpegPart Entropy(int start, int length)
        {
            return new JpegPart(0, start, length, start, length, true);
        }
    }

    private readonly record struct PngChunk(
        string Type,
        int Start,
        int TotalLength,
        int DataStart,
        int DataLength);

    private readonly record struct WebpChunk(
        string Type,
        int Start,
        int TotalLength,
        int DataStart,
        int DataLength);

    private sealed record ExifValues(
        string? WallClockText,
        string? OffsetText,
        bool UsedDigitized,
        double? Latitude,
        double? Longitude);
}
