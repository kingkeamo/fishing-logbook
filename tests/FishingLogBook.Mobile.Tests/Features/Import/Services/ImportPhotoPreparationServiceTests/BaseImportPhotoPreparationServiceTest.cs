using System.Text;
using FishingLogBook.Mobile.Features.Import.Models;
using FishingLogBook.Mobile.Features.Import.Services;
using FishingLogBook.Mobile.Features.Photographs.Models;
using FishingLogBook.Mobile.IO.Adapters;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Photographs.Contracts;
using FishingLogBook.Shared.Photographs.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Import.Services.ImportPhotoPreparationServiceTests;

public class BaseImportPhotoPreparationServiceTest
{
    protected const ushort DateTimeOriginalTag = 0x9003;
    protected const ushort OffsetTimeOriginalTag = 0x9011;

    protected static readonly DateTimeOffset FileModifiedOn =
        DateTimeOffset.Parse("2026-08-22T10:28:43+00:00");

    protected static readonly DateTimeOffset ReferenceNow =
        DateTimeOffset.Parse("2026-08-26T12:00:00Z");

    protected static TestContext CreateContext()
    {
        var mapper = Substitute.For<IPhotographLocalTimeMapper>();
        mapper.FromDateTimeLocalValueAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((DateTimeOffset?)null);
        var registry = Substitute.For<IImportPhotoBlobRegistryService>();
        registry.RegisterAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => new ImportPhotoBlobRegistrationModel(
                $"token-{call.ArgAt<byte[]>(0)[0]}",
                $"blob:thumbnail-{call.ArgAt<byte[]>(0)[0]}"));
        registry.ClearAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        registry.RemoveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var files = new MemoryFileSystem();
        var sut = new ImportPhotoPreparationService(
            new PhotographMetadataProcessor(mapper),
            registry,
            files,
            new FixedTimeProvider(ReferenceNow),
            NullLogger<ImportPhotoPreparationService>.Instance);
        return new TestContext(sut, registry, files);
    }

    protected static byte[] JpegWithoutExif()
    {
        return [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x04, 0x00, 0x00, 0xFF, 0xD9];
    }

    protected static byte[] Jpeg(ExifContent content, byte entropy = 0xE0)
    {
        var tiff = Tiff(content);
        var payload = new List<byte>();
        payload.AddRange("Exif\0\0"u8.ToArray());
        payload.AddRange(tiff);
        var bytes = new List<byte> { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };
        bytes.AddRange("JFIF\0"u8.ToArray());
        bytes.AddRange(new byte[9]);
        bytes.AddRange([0xFF, 0xE1]);
        bytes.AddRange(BigEndian((ushort)(payload.Count + 2)));
        bytes.AddRange(payload);
        bytes.AddRange([0xFF, 0xDB, 0x00, 0x05, 0x00, 0x01, 0x02]);
        bytes.AddRange([0xFF, 0xDA, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x3F, 0x00]);
        bytes.AddRange([0x9A, 0x2B, 0x7C, 0x11, 0x4D, entropy]);
        bytes.AddRange([0xFF, 0xD9]);
        return [.. bytes];
    }

    private static byte[] Tiff(ExifContent content)
    {
        var exifEntries = content.ExifText
            .Select(entry => AsciiEntry(entry.Key, entry.Value))
            .ToList();
        var gpsEntries = BuildGpsEntries(content);
        var rootEntries = new List<PendingEntry>();
        var rootSize = IfdSize(
            (exifEntries.Count > 0 ? 1 : 0) + (gpsEntries.Count > 0 ? 1 : 0));
        var exifOffset = 8 + rootSize;
        var gpsOffset = exifOffset + (exifEntries.Count > 0 ? IfdSize(exifEntries.Count) : 0);
        var dataOffset = gpsOffset + (gpsEntries.Count > 0 ? IfdSize(gpsEntries.Count) : 0);
        if (exifEntries.Count > 0)
        {
            rootEntries.Add(new PendingEntry(0x8769, 4, 1, LittleEndian((uint)exifOffset)));
        }

        if (gpsEntries.Count > 0)
        {
            rootEntries.Add(new PendingEntry(0x8825, 4, 1, LittleEndian((uint)gpsOffset)));
        }

        var data = new List<byte>();
        var bytes = new List<byte>();
        bytes.AddRange("II"u8.ToArray());
        bytes.AddRange(LittleEndian((ushort)42));
        bytes.AddRange(LittleEndian(8u));
        bytes.AddRange(WriteIfd(rootEntries, dataOffset, data));
        if (exifEntries.Count > 0)
        {
            bytes.AddRange(WriteIfd(exifEntries, dataOffset, data));
        }

        if (gpsEntries.Count > 0)
        {
            bytes.AddRange(WriteIfd(gpsEntries, dataOffset, data));
        }

        bytes.AddRange(data);
        return [.. bytes];
    }

    private static List<PendingEntry> BuildGpsEntries(ExifContent content)
    {
        var entries = new List<PendingEntry>();
        if (content.Latitude is null || content.Longitude is null)
        {
            return entries;
        }

        entries.Add(AsciiEntry(0x0001, content.Latitude >= 0 ? "N" : "S"));
        entries.Add(RationalEntry(0x0002, Math.Abs(content.Latitude.Value)));
        entries.Add(AsciiEntry(0x0003, content.Longitude >= 0 ? "E" : "W"));
        entries.Add(RationalEntry(0x0004, Math.Abs(content.Longitude.Value)));
        return entries;
    }

    private static int IfdSize(int entryCount)
    {
        return 2 + (entryCount * 12) + 4;
    }

    private static byte[] WriteIfd(List<PendingEntry> entries, int dataOffset, List<byte> data)
    {
        var bytes = new List<byte>();
        bytes.AddRange(LittleEndian((ushort)entries.Count));
        foreach (var entry in entries)
        {
            bytes.AddRange(LittleEndian(entry.Tag));
            bytes.AddRange(LittleEndian(entry.Type));
            bytes.AddRange(LittleEndian(entry.Count));
            if (entry.Value.Length <= 4)
            {
                var inline = new byte[4];
                entry.Value.CopyTo(inline, 0);
                bytes.AddRange(inline);
                continue;
            }

            bytes.AddRange(LittleEndian((uint)(dataOffset + data.Count)));
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

    private static PendingEntry RationalEntry(ushort tag, double degrees)
    {
        var whole = (uint)Math.Floor(degrees);
        var minutesTotal = (degrees - whole) * 60;
        var minutes = (uint)Math.Floor(minutesTotal);
        var seconds = (uint)Math.Round((minutesTotal - minutes) * 60 * 1000);
        var bytes = new List<byte>();
        bytes.AddRange(LittleEndian(whole));
        bytes.AddRange(LittleEndian(1u));
        bytes.AddRange(LittleEndian(minutes));
        bytes.AddRange(LittleEndian(1u));
        bytes.AddRange(LittleEndian(seconds));
        bytes.AddRange(LittleEndian(1000u));
        return new PendingEntry(tag, 5, 3, [.. bytes]);
    }

    protected static byte[] BigEndian(ushort value)
    {
        return [(byte)(value >> 8), (byte)value];
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
    }

    private sealed record PendingEntry(ushort Tag, ushort Type, uint Count, byte[] Value);

    protected sealed class TestContext(
        ImportPhotoPreparationService sut,
        IImportPhotoBlobRegistryService registry,
        MemoryFileSystem files)
    {
        private int _fileSequence;

        public ImportPhotoPreparationService Sut { get; } = sut;

        public IImportPhotoBlobRegistryService Registry { get; } = registry;

        public MemoryFileSystem Files { get; } = files;

        public PhotographPickedFileModel File(
            byte[] bytes,
            string contentType = PhotographContentTypeConstants.Jpeg,
            DateTimeOffset? lastModified = null,
            string name = "photo.jpg",
            bool includeLastModified = true)
        {
            var path = $"/import/{++_fileSequence}/{name}";
            Files.Add(path, bytes);
            return new PhotographPickedFileModel
            {
                FullPath = path,
                ContentType = contentType,
                LastModified = includeLastModified ? lastModified ?? FileModifiedOn : null
            };
        }
    }

    protected sealed class MemoryFileSystem : IFileSystemAdapter
    {
        private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);

        public void Add(string path, byte[] bytes)
        {
            _files[path] = bytes;
        }

        public bool FileExists(string path)
        {
            return _files.ContainsKey(path);
        }

        public Stream OpenRead(string path)
        {
            return new MemoryStream(_files[path], writable: false);
        }

        public Stream Create(string path) => throw new NotSupportedException();

        public void CreateDirectory(string path) => throw new NotSupportedException();

        public void MoveFile(string sourcePath, string destinationPath, bool overwrite) =>
            throw new NotSupportedException();

        public void DeleteFile(string path) => throw new NotSupportedException();

        public bool DirectoryExists(string path) => throw new NotSupportedException();

        public IEnumerable<string> EnumerateFiles(string directory, string searchPattern) =>
            throw new NotSupportedException();
    }

    protected sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return now;
        }
    }
}
