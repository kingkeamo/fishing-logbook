using System.Globalization;
using System.Text;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Features.Catch.Models;

namespace FishingLogBook.Web.Features.Catch.Services;

public sealed class PhotoMetadataService : IPhotoMetadataService
{
    private const int EarliestPlausibleCaptureYear = 1900;
    private const int TiffHeaderLength = 8;
    private const int IfdEntryLength = 12;
    private const int ExifIdentifierLength = 6;
    private const ushort ExifIfdPointerTag = 0x8769;
    private const ushort GpsIfdPointerTag = 0x8825;
    private const ushort DateTimeOriginalTag = 0x9003;
    private const ushort DateTimeDigitizedTag = 0x9004;
    private const ushort OffsetTimeOriginalTag = 0x9011;
    private const ushort OffsetTimeDigitizedTag = 0x9012;
    private const ushort GpsLatitudeRefTag = 0x0001;
    private const ushort GpsLatitudeTag = 0x0002;
    private const ushort GpsLongitudeRefTag = 0x0003;
    private const ushort GpsLongitudeTag = 0x0004;
    private const string ExifWallClockFormat = "yyyy:MM:dd HH:mm:ss";
    private const string ExifWallClockWithOffsetFormat = "yyyy:MM:dd HH:mm:sszzz";
    private const string DateTimeLocalFormat = "yyyy-MM-ddTHH:mm:ss";

    private static readonly byte[] ExifIdentifier = [0x45, 0x78, 0x69, 0x66, 0x00, 0x00];
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private readonly ITimeService _time;

    public PhotoMetadataService(ITimeService time)
    {
        _time = time;
    }

    public async Task<PhotoMetadataModel> ReadAsync(
        byte[] bytes,
        string contentType,
        CancellationToken cancellationToken)
    {
        var tiffOffset = FindTiffOffset(bytes, contentType);
        if (tiffOffset < 0)
        {
            return PhotoMetadataModel.Empty;
        }

        var values = ReadExifValues(bytes, tiffOffset);
        if (values is null)
        {
            return PhotoMetadataModel.Empty;
        }

        var capturedOn = await ResolveCapturedOnAsync(values, cancellationToken);
        return new PhotoMetadataModel(capturedOn, values.Latitude, values.Longitude);
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
        if (bytes.Length < 4 || bytes[0] != 0xFF || bytes[1] != 0xD8)
        {
            return -1;
        }

        var position = 2;
        while (position + 4 <= bytes.Length)
        {
            if (bytes[position] != 0xFF)
            {
                return -1;
            }

            var marker = bytes[position + 1];
            if (marker == 0xFF)
            {
                position++;
                continue;
            }

            if (IsStandaloneMarker(marker))
            {
                position += 2;
                continue;
            }

            if (IsTerminalMarker(marker))
            {
                return -1;
            }

            var length = ReadUInt16(bytes, position + 2, bigEndian: true);
            if (length < 2 || position + 2 + length > bytes.Length)
            {
                return -1;
            }

            if (marker == 0xE1 && StartsWithExifIdentifier(bytes, position + 4, length - 2))
            {
                return position + 4 + ExifIdentifierLength;
            }

            position += 2 + length;
        }

        return -1;
    }

    private static bool IsStandaloneMarker(byte marker)
    {
        return marker == 0x01 || marker == 0xD8 || marker is >= 0xD0 and <= 0xD7;
    }

    private static bool IsTerminalMarker(byte marker)
    {
        return marker == 0xDA || marker == 0xD9;
    }

    private static int FindPngTiffOffset(byte[] bytes)
    {
        if (!StartsWith(bytes, PngSignature))
        {
            return -1;
        }

        var position = PngSignature.Length;
        while (position + 8 <= bytes.Length)
        {
            var length = ReadUInt32(bytes, position, bigEndian: true);
            if (length > int.MaxValue - 12 || position + 12 + (int)length > bytes.Length)
            {
                return -1;
            }

            if (IsChunkType(bytes, position + 4, "eXIf"))
            {
                return SkipOptionalExifIdentifier(bytes, position + 8, (int)length);
            }

            position += 12 + (int)length;
        }

        return -1;
    }

    private static int FindWebpTiffOffset(byte[] bytes)
    {
        if (bytes.Length < 16 || !IsChunkType(bytes, 0, "RIFF") || !IsChunkType(bytes, 8, "WEBP"))
        {
            return -1;
        }

        var position = 12;
        while (position + 8 <= bytes.Length)
        {
            var size = ReadUInt32(bytes, position + 4, bigEndian: false);
            if (size > int.MaxValue - 9 || position + 8 + (int)size > bytes.Length)
            {
                return -1;
            }

            if (IsChunkType(bytes, position, "EXIF"))
            {
                return SkipOptionalExifIdentifier(bytes, position + 8, (int)size);
            }

            position += 8 + (int)size + ((int)size & 1);
        }

        return -1;
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
        var (wallClockText, offsetText) = ReadCaptureText(bytes, tiffOffset, exif, bigEndian);
        var (latitude, longitude) = ReadCoordinates(bytes, tiffOffset, gps, bigEndian);
        return wallClockText is null && latitude is null
            ? null
            : new ExifValues(wallClockText, offsetText, latitude, longitude);
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

    private static (string? WallClockText, string? OffsetText) ReadCaptureText(
        byte[] bytes,
        int tiffOffset,
        Dictionary<ushort, IfdEntry> exif,
        bool bigEndian)
    {
        var original = ReadAscii(bytes, tiffOffset, exif, DateTimeOriginalTag, bigEndian);
        if (original is not null)
        {
            return (original, ReadAscii(bytes, tiffOffset, exif, OffsetTimeOriginalTag, bigEndian));
        }

        var digitized = ReadAscii(bytes, tiffOffset, exif, DateTimeDigitizedTag, bigEndian);
        return digitized is null
            ? (null, null)
            : (digitized, ReadAscii(bytes, tiffOffset, exif, OffsetTimeDigitizedTag, bigEndian));
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

    private readonly record struct IfdEntry(ushort Type, uint Count, int ValuePosition);

    private sealed record ExifValues(
        string? WallClockText,
        string? OffsetText,
        double? Latitude,
        double? Longitude);
}
