using System.Buffers.Binary;
using System.Text;
using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Photographs.Constants;
using FishingLogBook.Mobile.Features.Photographs.Enums;
using FishingLogBook.Mobile.Features.Photographs.Providers;
using FishingLogBook.Mobile.Features.Photographs.Services;
using FishingLogBook.Mobile.Features.Photographs.Storage.Stores;
using FishingLogBook.Mobile.IO.Adapters;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Photographs.Contracts;
using FishingLogBook.Shared.Photographs.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Photographs.Services.PhotographPreparationServiceTests;

public class WhenTestingPrepareFromFile : IDisposable
{
    private readonly string _root;
    private readonly string _sourcePath;

    public WhenTestingPrepareFromFile()
    {
        _root = Path.Combine(Path.GetTempPath(), "flb-photo-prep", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _sourcePath = Path.Combine(_root, "source.jpg");
        File.WriteAllBytes(_sourcePath, [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x04, 0x00, 0x00, 0xFF, 0xD9]);
    }

    [Fact]
    public async Task ShouldSanitiseAndStoreDurableFile()
    {
        var userId = Guid.NewGuid();
        var fileStore = new PhotographFileStore(
            new FileSystemAdapter(),
            new FixedRoot(_root),
            NullLogger<PhotographFileStore>.Instance);
        var sut = CreateSut(fileStore);

        var result = await sut.PrepareFromFileAsync(
            userId,
            _sourcePath,
            PhotographContentTypeConstants.Jpeg,
            DateTimeOffset.UtcNow,
            PhotographSourceEnum.Gallery,
            CancellationToken.None);

        result.Outcome.Should().Be(PhotographPreparationOutcomeEnum.Prepared);
        result.File.Should().NotBeNull();
        File.Exists(result.File!.AbsolutePath).Should().BeTrue();
    }

    [Theory]
    [InlineData("image/heic")]
    [InlineData("image/jpeg")]
    public async Task ShouldRejectHeicBytesRegardlessOfDeclaredType(string contentType)
    {
        File.WriteAllBytes(_sourcePath, [0, 0, 0, 24, 0x66, 0x74, 0x79, 0x70, 0x68, 0x65, 0x69, 0x63, 0, 0, 0, 0]);
        var sut = CreateSut(new PhotographFileStore(
            new FileSystemAdapter(),
            new FixedRoot(_root),
            NullLogger<PhotographFileStore>.Instance));

        var result = await sut.PrepareFromFileAsync(
            Guid.NewGuid(),
            _sourcePath,
            contentType,
            null,
            PhotographSourceEnum.Gallery,
            CancellationToken.None);

        result.Outcome.Should().Be(PhotographPreparationOutcomeEnum.UnsupportedContentType);
    }

    [Theory]
    [InlineData(PhotographSourceEnum.Camera, "image/jpeg")]
    [InlineData(PhotographSourceEnum.Camera, "image/png")]
    [InlineData(PhotographSourceEnum.Camera, "application/octet-stream")]
    [InlineData(PhotographSourceEnum.Camera, "image/heic")]
    [InlineData(PhotographSourceEnum.Gallery, "image/png")]
    public async Task ShouldUsePngBytesForSanitisationMimeAndDurableExtension(PhotographSourceEnum source, string declaredType)
    {
        // Arrange
        var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+ip1sAAAAASUVORK5CYII=");
        File.WriteAllBytes(_sourcePath, bytes);
        var userId = Guid.NewGuid();
        var store = CreateFileStore();
        var sut = CreateSut(store);
        var timestamp = DateTimeOffset.UtcNow.AddDays(-2);

        // Act
        var result = await sut.PrepareFromFileAsync(userId, _sourcePath, declaredType, timestamp, source, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(PhotographPreparationOutcomeEnum.Prepared);
        result.File!.ContentType.Should().Be("image/png");
        result.File.RelativeFilePath.Should().EndWith(".png");
        File.Delete(_sourcePath);
        var restartedStore = CreateFileStore();
        restartedStore.TryResolveRelativePath(userId, result.File.RelativeFilePath, out var durablePath).Should().BeTrue();
        File.ReadAllBytes(durablePath).Should().Equal(bytes);
        result.Metadata.CapturedOn.Should().Be(source == PhotographSourceEnum.Camera ? null : timestamp);
        Directory.GetFiles(Path.GetDirectoryName(durablePath)!, "*.tmp").Should().BeEmpty();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public async Task ShouldPreserveJpegOrientationAndImageDataWhileRemovingPrivateMetadata(int orientation)
    {
        // Arrange
        var image = Convert.FromBase64String("/9j/4AAQSkZJRgABAQEAYABgAAD/2wBDAAMCAgMCAgMDAwMEAwMEBQgFBQQEBQoHBwYIDAoMDAsKCwsNDhIQDQ4RDgsLEBYQERMUFRUVDA8XGBYUGBIUFRT/2wBDAQMEBAUEBQkFBQkUDQsNFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBT/wAARCAADAAIDASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwD8qqKKKAP/2Q==");
        byte[] orientationSegment = [0xFF, 0xE1, 0, 34, 69, 120, 105, 102, 0, 0,
            73, 73, 42, 0, 8, 0, 0, 0, 1, 0, 0x12, 1, 3, 0, 1, 0, 0, 0, (byte)orientation, 0, 0, 0, 0, 0, 0, 0];
        var privatePayload = Encoding.ASCII.GetBytes("PRIVATE-GPS-CAMERA-OWNER");
        byte[] privateSegment = [0xFF, 0xE1, 0, (byte)(privatePayload.Length + 2), .. privatePayload];
        File.WriteAllBytes(_sourcePath, [.. image.Take(2), .. orientationSegment, .. privateSegment, .. image.Skip(2)]);
        var sut = CreateSut(CreateFileStore());

        // Act
        var result = await sut.PrepareFromFileAsync(Guid.NewGuid(), _sourcePath, "image/png", null, PhotographSourceEnum.Camera, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(PhotographPreparationOutcomeEnum.Prepared);
        result.File!.ContentType.Should().Be("image/jpeg");
        result.File.RelativeFilePath.Should().EndWith(".jpg");
        var saved = File.ReadAllBytes(result.File.AbsolutePath);
        var expected = orientation == 1 ? image : image.Take(20).Concat(orientationSegment).Concat(image.Skip(20)).ToArray();
        saved.Should().Equal(expected);
        Encoding.ASCII.GetString(saved).Should().NotContain("PRIVATE");
        result.Metadata.Should().Be(FishingLogBook.Shared.Photographs.Models.PhotographMetadataModel.Empty);
    }

    [Theory]
    [InlineData(0, false, PhotographPreparationOutcomeEnum.Prepared)]
    [InlineData(1, false, PhotographPreparationOutcomeEnum.TooLarge)]
    [InlineData(0, true, PhotographPreparationOutcomeEnum.Prepared)]
    [InlineData(1, true, PhotographPreparationOutcomeEnum.TooLarge)]
    public async Task ShouldEnforceTheExactByteLimitWithoutReturningGenericFailure(int excess, bool png, PhotographPreparationOutcomeEnum expected)
    {
        // Arrange
        var bytes = new byte[10 * 1024 * 1024 + excess];
        var image = png
            ? Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+ip1sAAAAASUVORK5CYII=")
            : new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
        image.CopyTo(bytes, 0);
        if (png)
        {
            var chunkStart = image.Length - 12;
            bytes.AsSpan(chunkStart).Clear();
            BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(chunkStart, 4), bytes.Length - image.Length - 12);
            "tEXt"u8.CopyTo(bytes.AsSpan(chunkStart + 4));
            "padding\0"u8.CopyTo(bytes.AsSpan(chunkStart + 8));
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(bytes.Length - 16, 4), PngCrc(bytes.AsSpan(chunkStart + 4, bytes.Length - 20 - chunkStart)));
            image.AsSpan(image.Length - 12).CopyTo(bytes.AsSpan(bytes.Length - 12));
        }
        File.WriteAllBytes(_sourcePath, bytes);
        var mockStore = Substitute.For<IPhotographFileStore>();
        var sut = CreateSut(excess == 0 ? CreateFileStore() : mockStore);

        // Act
        var result = await sut.PrepareFromFileAsync(Guid.NewGuid(), _sourcePath, "image/jpeg", null, PhotographSourceEnum.Camera, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(expected);
        await mockStore.DidNotReceiveWithAnyArgs().SaveAsync(default, default, default!, default!, default);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ShouldNotSaveMissingTruncatedOrEmptyCameraFiles(int input)
    {
        // Arrange
        if (input == 0)
        {
            File.Delete(_sourcePath);
        }
        else
        {
            File.WriteAllBytes(_sourcePath, input == 1 ? [0xFF, 0xD8, 0xFF, 0xE1, 0, 100] : []);
        }
        var mockStore = Substitute.For<IPhotographFileStore>();
        var sut = CreateSut(mockStore);

        // Act
        var result = await sut.PrepareFromFileAsync(Guid.NewGuid(), _sourcePath, "image/jpeg", null, PhotographSourceEnum.Camera, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(PhotographPreparationOutcomeEnum.CouldNotPrepare);
        result.File.Should().BeNull();
        await mockStore.DidNotReceiveWithAnyArgs().SaveAsync(default, default, default!, default!, default);
    }

    [Fact]
    public async Task ShouldReportDurableSaveFailureWithSafeStageDiagnostics()
    {
        // Arrange
        var mockStore = Substitute.For<IPhotographFileStore>();
        var mockLogger = Substitute.For<ILogger<PhotographPreparationService>>();
        var userId = Guid.NewGuid();
        mockStore.SaveAsync(userId, Arg.Any<Guid>(), "image/jpeg", Arg.Any<byte[]>(), CancellationToken.None)
            .Returns(Task.FromResult<string?>(null));
        var sut = CreateSut(mockStore, mockLogger);

        // Act
        var result = await sut.PrepareFromFileAsync(userId, _sourcePath, "PRIVATE-UNTRUSTED-MIME", null, PhotographSourceEnum.Camera, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(PhotographPreparationOutcomeEnum.CouldNotPrepare);
        result.File.Should().BeNull();
        await mockStore.Received(1).SaveAsync(userId, Arg.Is<Guid>(id => id != Guid.Empty), "image/jpeg",
            Arg.Is<byte[]>(bytes => bytes.SequenceEqual(File.ReadAllBytes(_sourcePath))), CancellationToken.None);
        mockStore.DidNotReceiveWithAnyArgs().GetAbsolutePath(default, default!);
        var messages = string.Join("\n", mockLogger.ReceivedCalls().Select(call => call.GetArguments()[2]?.ToString()));
        messages.Should().Contain("durable save").And.Contain("image/jpeg").And.NotContain("PRIVATE").And.NotContain(_sourcePath);
    }

    [Theory]
    [InlineData(PhotographSourceEnum.Camera)]
    [InlineData(PhotographSourceEnum.Gallery)]
    public async Task ShouldPreserveSupportedWebpBytesWithIncorrectDeclaredMime(PhotographSourceEnum source)
    {
        // Arrange
        var bytes = Convert.FromBase64String("UklGRiIAAABXRUJQVlA4IBYAAAAwAQCdASoBAAEADsD+JaQAA3AAAAAA");
        File.WriteAllBytes(_sourcePath, bytes);
        var sut = CreateSut(CreateFileStore());

        // Act
        var result = await sut.PrepareFromFileAsync(Guid.NewGuid(), _sourcePath, "image/jpeg", null, source, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(PhotographPreparationOutcomeEnum.Prepared);
        result.File!.ContentType.Should().Be("image/webp");
        result.File.RelativeFilePath.Should().EndWith(".webp");
        File.ReadAllBytes(result.File.AbsolutePath).Should().Equal(bytes);
    }

    [Fact]
    public async Task ShouldBoundReadsFromNonSeekableSourcesAndNeverSaveOversizedData()
    {
        // Arrange
        var mockFileSystem = Substitute.For<IFileSystemAdapter>();
        var mockStore = Substitute.For<IPhotographFileStore>();
        var stream = new NonSeekableReadStream(new byte[11 * 1024 * 1024]);
        mockFileSystem.FileExists(_sourcePath).Returns(true);
        mockFileSystem.OpenRead(_sourcePath).Returns(stream);
        var mapper = Substitute.For<IPhotographLocalTimeMapper>();
        var sut = new PhotographPreparationService(new PhotographMetadataProcessor(mapper), mockStore, mockFileSystem,
            TimeProvider.System, NullLogger<PhotographPreparationService>.Instance);

        // Act
        var result = await sut.PrepareFromFileAsync(Guid.NewGuid(), _sourcePath, "image/png", null, PhotographSourceEnum.Camera, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(PhotographPreparationOutcomeEnum.TooLarge);
        stream.BytesRead.Should().Be(sut.MaxPhotographBytes + 1);
        mockFileSystem.Received(1).OpenRead(_sourcePath);
        await mockStore.DidNotReceiveWithAnyArgs().SaveAsync(default, default, default!, default!, default);
    }

    private static uint PngCrc(ReadOnlySpan<byte> bytes)
    {
        var crc = uint.MaxValue;
        foreach (var value in bytes)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) == 1 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
            }
        }

        return ~crc;
    }

    private PhotographFileStore CreateFileStore()
    {
        return new PhotographFileStore(new FileSystemAdapter(), new FixedRoot(_root), NullLogger<PhotographFileStore>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static PhotographPreparationService CreateSut(IPhotographFileStore fileStore, ILogger<PhotographPreparationService>? logger = null)
    {
        var mapper = Substitute.For<IPhotographLocalTimeMapper>();
        mapper.FromDateTimeLocalValueAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(DateTimeOffset.UtcNow);
        return new PhotographPreparationService(
            new PhotographMetadataProcessor(mapper),
            fileStore,
            new FileSystemAdapter(),
            TimeProvider.System,
            logger ?? NullLogger<PhotographPreparationService>.Instance);
    }

    private sealed class FixedRoot(string root) : IPhotographRootPathProvider
    {
        public string GetPhotographRootDirectory() => root;
    }

    private sealed class NonSeekableReadStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek { get; } = false;

        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        public long BytesRead { get; private set; }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var read = await base.ReadAsync(buffer, cancellationToken);
            BytesRead += read;
            return read;
        }
    }
}
