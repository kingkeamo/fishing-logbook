using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Features.Import.Enums;
using FishingLogBook.Web.Features.Import.Models;
using FishingLogBook.Web.Features.Import.Services;
using FishingLogBook.Web.Features.Photographs.Enums;
using FishingLogBook.Web.Features.Photographs.Models;
using Microsoft.AspNetCore.Components.Forms;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Import.Services.ImportPhotoPreparationServiceTests;

public class WhenTestingPrepareSelectionAsync : BaseImportPhotoPreparationServiceTest
{
    [Fact]
    public async Task ItShouldRejectMoreThanTheConfiguredMaximum()
    {
        // Arrange
        var context = CreateContext();
        var files = Enumerable.Range(0, ImportPhotoPreparationService.MaxPhotographs + 1)
            .Select(_ => File(OriginalBytes))
            .ToArray();

        // Act
        var act = () => context.Sut.PrepareSelectionAsync(files, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        await context.Registry.DidNotReceive().ClearAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRetainUnsupportedAndOversizedFilesAsFailures()
    {
        // Arrange
        var context = CreateContext();
        var oversized = Substitute.For<IBrowserFile>();
        oversized.Name.Returns("large.jpg");
        oversized.ContentType.Returns(PhotographContentTypeConstants.Jpeg);
        oversized.Size.Returns(ImportPhotoPreparationService.MaxPhotographBytes + 1);
        IBrowserFile[] files = [File(OriginalBytes, "image/heic"), oversized];

        // Act
        var result = await context.Sut.PrepareSelectionAsync(files, CancellationToken.None);

        // Assert
        result.Select(photo => photo.PreparationStatus).Should().Equal(
            ImportPhotoPreparationStatusEnum.UnsupportedType,
            ImportPhotoPreparationStatusEnum.TooLarge);
        result.Should().OnlyContain(photo => !photo.IsReady);
        context.Metadata.DidNotReceive().ReadHistorical(
            Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset>());
    }

    [Fact]
    public async Task ItShouldMapExplicitOffsetGpsAndSanitisedBlobInSelectionOrder()
    {
        // Arrange
        var historical = new PhotographHistoricalMetadataModel(
            DateTimeOffset.Parse("2025-06-14T09:30:00+01:00"),
            null,
            PhotographCapturedOnSourceEnum.ExifOriginal,
            true,
            false,
            53.3498,
            -6.2603);
        var context = CreateContext(historical);
        var files = new[]
        {
            File([1], name: "first.jpg"),
            File([2], name: "second.jpg")
        };

        // Act
        var result = await context.Sut.PrepareSelectionAsync(files, CancellationToken.None);

        // Assert
        result.Select(photo => photo.SelectionIndex).Should().Equal(0, 1);
        result.Select(photo => photo.FileName).Should().Equal("first.jpg", "second.jpg");
        result.Should().OnlyContain(photo => photo.IsReady);
        result[0].Timestamp.State.Should().Be(ImportTimestampStateEnum.ExplicitInstant);
        result[0].Location.HasCanonicalCoordinates.Should().BeTrue();
        await context.Registry.Received(2).RegisterAsync(
            Arg.Is<byte[]>(bytes => bytes.SequenceEqual(SanitisedBytes)),
            PhotographContentTypeConstants.Jpeg,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldMapAmbiguousWeakMalformedAndMissingTimestamps()
    {
        // Arrange
        var variants = new[]
        {
            new PhotographHistoricalMetadataModel(null, new DateTime(2025, 6, 14, 9, 30, 0), PhotographCapturedOnSourceEnum.ExifOriginal, true, false, null, null),
            new PhotographHistoricalMetadataModel(FileModifiedOn, null, PhotographCapturedOnSourceEnum.FileLastModified, false, false, null, null),
            new PhotographHistoricalMetadataModel(null, null, PhotographCapturedOnSourceEnum.None, true, true, null, null),
            MissingMetadata()
        };
        var context = CreateContext();
        var index = 0;
        context.Metadata.ReadHistorical(
                Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset>())
            .Returns(_ => variants[index++]);
        var files = Enumerable.Range(0, variants.Length).Select(value => File([(byte)value])).ToArray();

        // Act
        var result = await context.Sut.PrepareSelectionAsync(files, CancellationToken.None);

        // Assert
        result.Select(photo => photo.Timestamp.State).Should().Equal(
            ImportTimestampStateEnum.LocalWallClock,
            ImportTimestampStateEnum.WeakFallback,
            ImportTimestampStateEnum.Unusable,
            ImportTimestampStateEnum.Missing);
    }

    [Fact]
    public async Task ItShouldContinueWithSanitisedPhotoWhenMetadataExtractionFails()
    {
        // Arrange
        var context = CreateContext();
        context.Metadata.ReadHistorical(
                Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset>())
            .Returns(_ => throw new InvalidDataException("private metadata detail"));

        // Act
        var result = await context.Sut.PrepareSelectionAsync([File(OriginalBytes)], CancellationToken.None);

        // Assert
        result.Single().IsReady.Should().BeTrue();
        result.Single().MetadataStatus.Should().Be(ImportMetadataStatusEnum.Failed);
        result.Single().MetadataError.Should().Be("metadata-unavailable");
        await context.Logging.Received(1).LogErrorAsync(
            "preparing a historical photograph",
            Arg.Is<string>(message =>
                message.Contains(nameof(InvalidDataException), StringComparison.Ordinal)
                && !message.Contains("private metadata detail", StringComparison.Ordinal)),
            CancellationToken.None);
    }

    [Fact]
    public async Task ItShouldExtractMetadataBeforeSanitisingAndRegisteringBytes()
    {
        // Arrange
        var context = CreateContext();

        // Act
        await context.Sut.PrepareSelectionAsync([File(OriginalBytes)], CancellationToken.None);

        // Assert
        Received.InOrder(() =>
        {
            context.Metadata.ReadHistorical(
                Arg.Is<byte[]>(bytes => bytes.SequenceEqual(OriginalBytes)),
                PhotographContentTypeConstants.Jpeg,
                Arg.Any<DateTimeOffset?>(),
                Arg.Any<DateTimeOffset>());
            context.Metadata.Sanitise(
                Arg.Is<byte[]>(bytes => bytes.SequenceEqual(OriginalBytes)),
                PhotographContentTypeConstants.Jpeg);
            context.Registry.RegisterAsync(
                Arg.Is<byte[]>(bytes => bytes.SequenceEqual(SanitisedBytes)),
                PhotographContentTypeConstants.Jpeg,
                Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task ItShouldClearRegisteredResourcesWhenCancelled()
    {
        // Arrange
        var context = CreateContext();
        using var cancellation = new CancellationTokenSource();
        var registry = new CancellingBlobRegistry(cancellation);
        var sut = new ImportPhotoPreparationService(context.Metadata, registry, context.Logging);
        var files = new[] { File([1]), File([2]) };

        // Act
        var act = () => sut.PrepareSelectionAsync(files, cancellation.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        registry.RegistrationCount.Should().Be(1);
        registry.ClearCount.Should().Be(2);
        registry.ActiveEntries.Should().BeEmpty();
        registry.ActiveThumbnailUrls.Should().BeEmpty();
        context.Metadata.Received(1).ReadHistorical(
            Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset>());
    }

    [Fact]
    public async Task ItShouldRemoveTheBlobBeforeMarkingAPhotoRemoved()
    {
        // Arrange
        var context = CreateContext();
        var photo = (await context.Sut.PrepareSelectionAsync(
            [File(OriginalBytes)],
            CancellationToken.None)).Single();

        // Act
        await context.Sut.RemoveAsync(photo, CancellationToken.None);

        // Assert
        photo.IsRemoved.Should().BeTrue();
        await context.Registry.Received(1).RemoveAsync(
            Arg.Is<string>(token => token == photo.BlobToken),
            Arg.Any<CancellationToken>());
    }

    private sealed class CancellingBlobRegistry(CancellationTokenSource cancellation)
        : IImportPhotoBlobRegistryService
    {
        private readonly Dictionary<string, string> _entries = [];

        public int RegistrationCount { get; private set; }

        public int ClearCount { get; private set; }

        public IReadOnlyDictionary<string, string> ActiveEntries => _entries;

        public IReadOnlyCollection<string> ActiveThumbnailUrls => _entries.Values;

        public Task<ImportPhotoBlobRegistrationModel> RegisterAsync(
            byte[] bytes,
            string contentType,
            CancellationToken cancellationToken)
        {
            RegistrationCount++;
            var registration = new ImportPhotoBlobRegistrationModel(
                $"token-{RegistrationCount}",
                $"blob:thumbnail-{RegistrationCount}");
            _entries.Add(registration.Token, registration.ThumbnailUrl);
            cancellation.Cancel();
            return Task.FromResult(registration);
        }

        public Task<byte[]> GetBytesAsync(string token, CancellationToken cancellationToken) =>
            Task.FromResult(Array.Empty<byte>());

        public Task RemoveAsync(string token, CancellationToken cancellationToken)
        {
            _entries.Remove(token);
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken)
        {
            ClearCount++;
            _entries.Clear();
            return Task.CompletedTask;
        }
    }
}
