using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Photographs.Constants;
using FishingLogBook.Mobile.Features.Photographs.Enums;
using FishingLogBook.Mobile.Features.Photographs.Services;
using FishingLogBook.Mobile.IO.Adapters;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Photographs.Contracts;
using FishingLogBook.Shared.Photographs.Services;
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

    [Fact]
    public async Task ShouldRejectHeicContentType()
    {
        var sut = CreateSut(new PhotographFileStore(
            new FileSystemAdapter(),
            new FixedRoot(_root),
            NullLogger<PhotographFileStore>.Instance));

        var result = await sut.PrepareFromFileAsync(
            Guid.NewGuid(),
            _sourcePath,
            PhotographHeicContentTypeConstants.Heic,
            null,
            PhotographSourceEnum.Gallery,
            CancellationToken.None);

        result.Outcome.Should().Be(PhotographPreparationOutcomeEnum.UnsupportedContentType);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static PhotographPreparationService CreateSut(IPhotographFileStore fileStore)
    {
        var mapper = Substitute.For<IPhotographLocalTimeMapper>();
        mapper.FromDateTimeLocalValueAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(DateTimeOffset.UtcNow);
        return new PhotographPreparationService(
            new PhotographMetadataProcessor(mapper),
            fileStore,
            new FileSystemAdapter(),
            TimeProvider.System,
            NullLogger<PhotographPreparationService>.Instance);
    }

    private sealed class FixedRoot(string root) : IPhotographRootPathProvider
    {
        public string GetPhotographRootDirectory() => root;
    }
}
