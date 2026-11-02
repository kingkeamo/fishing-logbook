using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Import.Enums;
using FishingLogBook.Mobile.Features.Import.Models;
using FishingLogBook.Mobile.Features.Import.Services;
using FishingLogBook.Mobile.Features.Photographs.Constants;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Photographs.Contracts;
using FishingLogBook.Shared.Photographs.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Mobile.Tests.Features.Import.Services.ImportPhotoPreparationServiceTests;

public class WhenTestingPrepareSelectionAsync : BaseImportPhotoPreparationServiceTest
{
    [Fact]
    public async Task ItShouldRequireResolutionForIdenticalPreparedBytesRegardlessOfFileName()
    {
        // Arrange
        var context = CreateContext();
        var bytes = JpegWithoutExif();
        var files = new[]
        {
            context.File(bytes, name: "first.jpg"),
            context.File(bytes, name: "copy.jpg")
        };

        // Act
        var result = await context.Sut.PrepareSelectionAsync(files, CancellationToken.None);

        // Assert
        result[0].Fingerprint.Should().Be(result[1].Fingerprint);
        result[0].RequiresDuplicateDecision.Should().BeFalse();
        result[1].DuplicateStatus.Should().Be(ImportDuplicateStatusEnum.Duplicate);
        result[1].DuplicateReason.Should().Be(ImportDuplicateReasonEnum.IdenticalPreparedBytes);
        result[1].DuplicatePhotoIds.Should().ContainSingle().Which.Should().Be(result[0].Id);
        result[1].RequiresDuplicateDecision.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldNotTreatMatchingMetadataOrThumbnailAsAnExactDuplicateWhenPreparedBytesDiffer()
    {
        // Arrange
        var context = CreateContext();
        context.Registry.RegisterAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => new ImportPhotoBlobRegistrationModel("token", "blob:same-thumbnail"));
        var files = new[]
        {
            context.File(ExplicitJpeg(entropy: 0xE0), name: "photo.jpg"),
            context.File(ExplicitJpeg(entropy: 0xE1), name: "photo.jpg")
        };

        // Act
        var result = await context.Sut.PrepareSelectionAsync(files, CancellationToken.None);

        // Assert
        result.Select(photo => photo.Fingerprint).Distinct().Should().HaveCount(2);
        result.Select(photo => photo.ThumbnailUrl).Distinct().Should().ContainSingle();
        result.Should().NotContain(photo => photo.DuplicateStatus == ImportDuplicateStatusEnum.Duplicate);
        result[1].DuplicateStatus.Should().Be(ImportDuplicateStatusEnum.Warning);
        result[1].DuplicateReason.Should().Be(ImportDuplicateReasonEnum.SameCaptureTimeAndSize);
    }

    [Fact]
    public async Task ItShouldNotWarnForFileNameAlone()
    {
        // Arrange
        var context = CreateContext();
        var files = new[]
        {
            context.File(JpegWithoutExif(), name: "photo.jpg"),
            context.File(ExplicitJpeg(entropy: 0xE0), name: "photo.jpg")
        };

        // Act
        var result = await context.Sut.PrepareSelectionAsync(files, CancellationToken.None);

        // Assert
        result.Should().OnlyContain(photo => photo.DuplicateStatus == ImportDuplicateStatusEnum.None);
        result.Should().OnlyContain(photo => !photo.RequiresDuplicateDecision);
    }

    [Fact]
    public async Task ItShouldNeverTreatGpsAloneAsDuplicateEvidence()
    {
        // Arrange
        var context = CreateContext();
        var files = new[]
        {
            context.File(ExplicitJpeg("2025:06:14 09:30:00", 53.3498, -6.2603, 0xE0)),
            context.File(ExplicitJpeg("2025:06:14 10:30:00", 53.3498, -6.2603, 0xE1))
        };

        // Act
        var result = await context.Sut.PrepareSelectionAsync(files, CancellationToken.None);

        // Assert
        result.Should().OnlyContain(photo => photo.DuplicateStatus == ImportDuplicateStatusEnum.None);
        result.Should().OnlyContain(photo => !photo.RequiresDuplicateDecision);
    }

    [Fact]
    public async Task ItShouldPrepareTheFullTwentyPhotoBatchSequentially()
    {
        // Arrange
        var context = CreateContext();
        var registry = new ConcurrencyTrackingBlobRegistry();
        var sut = new ImportPhotoPreparationService(
            new PhotographMetadataProcessor(Substitute.For<IPhotographLocalTimeMapper>()),
            registry,
            context.Files,
            new FixedTimeProvider(ReferenceNow),
            NullLogger<ImportPhotoPreparationService>.Instance);
        var files = Enumerable.Range(0, ImportPhotoPreparationService.MaxPhotographs)
            .Select(index => context.File(JpegWithoutExif(), name: $"photo-{index}.jpg"))
            .ToArray();

        // Act
        var result = await sut.PrepareSelectionAsync(files, CancellationToken.None);

        // Assert
        result.Should().HaveCount(20);
        result.Should().OnlyContain(photo => photo.IsReady && photo.BlobToken != null);
        result.Select(photo => photo.SelectionIndex).Should().Equal(Enumerable.Range(0, 20));
        result.Count(photo => photo.RequiresDuplicateDecision).Should().Be(19);
        registry.RegistrationCount.Should().Be(20);
        registry.MaximumConcurrency.Should().Be(1);
    }

    [Fact]
    public async Task ItShouldRejectMoreThanTheConfiguredMaximum()
    {
        // Arrange
        var context = CreateContext();
        var files = Enumerable.Range(0, ImportPhotoPreparationService.MaxPhotographs + 1)
            .Select(_ => context.File(JpegWithoutExif()))
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
        var oversized = new byte[ImportPhotoPreparationService.MaxPhotographBytes + 1];
        Array.Copy(JpegWithoutExif(), oversized, JpegWithoutExif().Length);
        var files = new[]
        {
            context.File(JpegWithoutExif(), PhotographHeicContentTypeConstants.Heic, name: "phone.heic"),
            context.File(oversized, name: "large.jpg")
        };

        // Act
        var result = await context.Sut.PrepareSelectionAsync(files, CancellationToken.None);

        // Assert
        result.Select(photo => photo.PreparationStatus).Should().Equal(
            ImportPhotoPreparationStatusEnum.UnsupportedType,
            ImportPhotoPreparationStatusEnum.TooLarge);
        result.Should().OnlyContain(photo => !photo.IsReady);
        await context.Registry.DidNotReceive().RegisterAsync(
            Arg.Any<byte[]>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldMapExplicitOffsetGpsAndSanitisedBlobInSelectionOrder()
    {
        // Arrange
        var context = CreateContext();
        var files = new[]
        {
            context.File(ExplicitJpeg("2025:06:14 09:30:00", 53.3498, -6.2603, 0xE0), name: "first.jpg"),
            context.File(ExplicitJpeg("2025:06:14 09:30:00", 53.3498, -6.2603, 0xE1), name: "second.jpg")
        };

        // Act
        var result = await context.Sut.PrepareSelectionAsync(files, CancellationToken.None);

        // Assert
        result.Select(photo => photo.SelectionIndex).Should().Equal(0, 1);
        result.Select(photo => photo.FileName).Should().Equal("first.jpg", "second.jpg");
        result.Should().OnlyContain(photo => photo.IsReady);
        result[0].Timestamp.State.Should().Be(ImportTimestampStateEnum.ExplicitInstant);
        result[0].Timestamp.Instant.Should().Be(DateTimeOffset.Parse("2025-06-14T09:30:00+01:00"));
        result[0].Location.HasCanonicalCoordinates.Should().BeTrue();
        result[0].Location.Latitude.Should().BeApproximately(53.3498, 0.0001);
        result[0].Location.Longitude.Should().BeApproximately(-6.2603, 0.0001);
        await context.Registry.Received(2).RegisterAsync(
            Arg.Is<byte[]>(bytes => bytes.Length > 0 && !ContainsExifIdentifier(bytes)),
            PhotographContentTypeConstants.Jpeg,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldMapAmbiguousWeakMalformedAndMissingTimestamps()
    {
        // Arrange
        var context = CreateContext();
        var files = new[]
        {
            context.File(Jpeg(new ExifContent
            {
                ExifText = { [DateTimeOriginalTag] = "2025:06:14 09:30:00" }
            })),
            context.File(JpegWithoutExif(), lastModified: FileModifiedOn),
            context.File(Jpeg(new ExifContent
            {
                ExifText = { [DateTimeOriginalTag] = "not-a-date" }
            })),
            context.File(JpegWithoutExif(), includeLastModified: false)
        };

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
    public async Task ItShouldExtractMetadataBeforeSanitisingAndRegisteringBytes()
    {
        // Arrange
        var context = CreateContext();
        var original = ExplicitJpeg("2025:06:14 09:30:00", 53.3498, -6.2603);

        // Act
        var result = await context.Sut.PrepareSelectionAsync(
            [context.File(original)],
            CancellationToken.None);

        // Assert
        result.Single().Timestamp.State.Should().Be(ImportTimestampStateEnum.ExplicitInstant);
        result.Single().Location.HasCanonicalCoordinates.Should().BeTrue();
        ContainsExifIdentifier(original).Should().BeTrue();
        await context.Registry.Received(1).RegisterAsync(
            Arg.Is<byte[]>(bytes => !ContainsExifIdentifier(bytes)),
            PhotographContentTypeConstants.Jpeg,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldClearRegisteredResourcesWhenCancelled()
    {
        // Arrange
        var context = CreateContext();
        using var cancellation = new CancellationTokenSource();
        var registry = new CancellingBlobRegistry(cancellation);
        var sut = new ImportPhotoPreparationService(
            new PhotographMetadataProcessor(Substitute.For<IPhotographLocalTimeMapper>()),
            registry,
            context.Files,
            new FixedTimeProvider(ReferenceNow),
            NullLogger<ImportPhotoPreparationService>.Instance);
        var files = new[]
        {
            context.File(JpegWithoutExif()),
            context.File(JpegWithoutExif())
        };

        // Act
        var act = () => sut.PrepareSelectionAsync(files, cancellation.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        registry.RegistrationCount.Should().Be(1);
        registry.ClearCount.Should().Be(2);
        registry.ActiveEntries.Should().BeEmpty();
        registry.ActiveThumbnailUrls.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldRetainInvalidFilesAsPreparationFailures()
    {
        // Arrange
        var context = CreateContext();

        // Act
        var result = await context.Sut.PrepareSelectionAsync(
            [context.File([1, 2, 3], name: "broken.jpg")],
            CancellationToken.None);

        // Assert
        result.Single().PreparationStatus.Should().Be(ImportPhotoPreparationStatusEnum.PreparationFailed);
        result.Single().IsReady.Should().BeFalse();
        await context.Registry.DidNotReceive().RegisterAsync(
            Arg.Any<byte[]>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAllowRetryAfterAFailedPreparation()
    {
        // Arrange
        var context = CreateContext();
        var failed = await context.Sut.PrepareSelectionAsync(
            [context.File([1, 2, 3], name: "broken.jpg")],
            CancellationToken.None);

        // Act
        var retried = await context.Sut.PrepareSelectionAsync(
            [context.File(JpegWithoutExif(), name: "ok.jpg")],
            CancellationToken.None);

        // Assert
        failed.Single().IsReady.Should().BeFalse();
        retried.Single().IsReady.Should().BeTrue();
        await context.Registry.Received(1).RegisterAsync(
            Arg.Any<byte[]>(),
            PhotographContentTypeConstants.Jpeg,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldTreatARegistryFailureAsPreparationFailed()
    {
        // Arrange
        var context = CreateContext();
        context.Registry.RegisterAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new IOException("disk full"));

        // Act
        var result = await context.Sut.PrepareSelectionAsync(
            [context.File(JpegWithoutExif())],
            CancellationToken.None);

        // Assert
        result.Single().PreparationStatus.Should().Be(ImportPhotoPreparationStatusEnum.PreparationFailed);
        result.Single().IsReady.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldRemoveTheBlobBeforeMarkingAPhotoRemoved()
    {
        // Arrange
        var context = CreateContext();
        var photo = (await context.Sut.PrepareSelectionAsync(
            [context.File(JpegWithoutExif())],
            CancellationToken.None)).Single();

        // Act
        await context.Sut.RemoveAsync(photo, CancellationToken.None);

        // Assert
        photo.IsRemoved.Should().BeTrue();
        await context.Registry.Received(1).RemoveAsync(
            Arg.Is<string>(token => token == photo.BlobToken),
            Arg.Any<CancellationToken>());
    }

    private static byte[] ExplicitJpeg(
        string wallClock = "2025:06:14 09:30:00",
        double? latitude = null,
        double? longitude = null,
        byte entropy = 0xE0)
    {
        return Jpeg(new ExifContent
        {
            ExifText =
            {
                [DateTimeOriginalTag] = wallClock,
                [OffsetTimeOriginalTag] = "+01:00"
            },
            Latitude = latitude,
            Longitude = longitude
        }, entropy);
    }

    private static bool ContainsExifIdentifier(byte[] bytes)
    {
        var marker = "Exif"u8;
        for (var index = 0; index <= bytes.Length - marker.Length; index++)
        {
            var match = true;
            for (var offset = 0; offset < marker.Length; offset++)
            {
                if (bytes[index + offset] != marker[offset])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                return true;
            }
        }

        return false;
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

        public Task<byte[]> GetBytesAsync(string token, CancellationToken cancellationToken)
        {
            return Task.FromResult(Array.Empty<byte>());
        }

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

    private sealed class ConcurrencyTrackingBlobRegistry : IImportPhotoBlobRegistryService
    {
        private int _activeRegistrations;

        public int RegistrationCount { get; private set; }

        public int MaximumConcurrency { get; private set; }

        public async Task<ImportPhotoBlobRegistrationModel> RegisterAsync(
            byte[] bytes,
            string contentType,
            CancellationToken cancellationToken)
        {
            var active = Interlocked.Increment(ref _activeRegistrations);
            MaximumConcurrency = Math.Max(MaximumConcurrency, active);
            await Task.Yield();
            RegistrationCount++;
            Interlocked.Decrement(ref _activeRegistrations);
            return new ImportPhotoBlobRegistrationModel(
                $"token-{RegistrationCount}",
                $"blob:thumbnail-{RegistrationCount}");
        }

        public Task<byte[]> GetBytesAsync(string token, CancellationToken cancellationToken)
        {
            return Task.FromResult(Array.Empty<byte>());
        }

        public Task RemoveAsync(string token, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
