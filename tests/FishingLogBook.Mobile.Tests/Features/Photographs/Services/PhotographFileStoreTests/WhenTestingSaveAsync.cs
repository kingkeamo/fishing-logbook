using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Photographs.Services;
using FishingLogBook.Mobile.IO.Adapters;
using FishingLogBook.Shared.Constants;
using Microsoft.Extensions.Logging.Abstractions;

namespace FishingLogBook.Mobile.Tests.Features.Photographs.Services.PhotographFileStoreTests;

public class WhenTestingSaveAsync : IDisposable
{
    private readonly string _root;
    private readonly PhotographFileStore _sut;

    public WhenTestingSaveAsync()
    {
        _root = Path.Combine(Path.GetTempPath(), "flb-photo-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _sut = new PhotographFileStore(
            new FileSystemAdapter(),
            new FixedPhotographRootPathProvider(_root),
            NullLogger<PhotographFileStore>.Instance);
    }

    [Fact]
    public async Task ShouldPersistRelativePathAndSurviveRestart()
    {
        var userId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var photographId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var bytes = MinimalJpeg();

        var relative = await _sut.SaveAsync(
            userId,
            photographId,
            PhotographContentTypeConstants.Jpeg,
            bytes,
            CancellationToken.None);
        relative.Should().NotBeNullOrWhiteSpace();
        relative.Should().Be($"{userId:N}/{photographId:N}.jpg");

        var restarted = new PhotographFileStore(
            new FileSystemAdapter(),
            new FixedPhotographRootPathProvider(_root),
            NullLogger<PhotographFileStore>.Instance);
        restarted.TryResolveRelativePath(userId, relative!, out var absolute).Should().BeTrue();
        File.ReadAllBytes(absolute).Should().Equal(bytes);
    }

    [Fact]
    public async Task ShouldIsolateAccounts()
    {
        var owner = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var other = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var ownerPhoto = Guid.NewGuid();
        var otherPhoto = Guid.NewGuid();
        var ownerRelative = await _sut.SaveAsync(
            owner,
            ownerPhoto,
            PhotographContentTypeConstants.Jpeg,
            MinimalJpeg(),
            CancellationToken.None);
        var otherRelative = await _sut.SaveAsync(
            other,
            otherPhoto,
            PhotographContentTypeConstants.Jpeg,
            MinimalJpeg(),
            CancellationToken.None);

        ownerRelative.Should().StartWith($"{owner:N}/");
        otherRelative.Should().StartWith($"{other:N}/");
        _sut.GetAccountDirectory(owner).Should().NotBe(_sut.GetAccountDirectory(other));
    }

    [Fact]
    public void ShouldRejectPathTraversalWhenResolving()
    {
        var userId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        _sut.TryResolveRelativePath(userId, "../escape.jpg", out _).Should().BeFalse();
        _sut.TryResolveRelativePath(
                userId,
                $"{userId:N}/../../other/escape.jpg",
                out _)
            .Should().BeFalse();
        var act = () => _sut.GetAbsolutePath(userId, "../escape.jpg");
        act.Should().Throw<InvalidOperationException>();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static byte[] MinimalJpeg()
    {
        return [0xFF, 0xD8, 0xFF, 0xD9];
    }

    private sealed class FixedPhotographRootPathProvider(string root) : IPhotographRootPathProvider
    {
        public string GetPhotographRootDirectory() => root;
    }
}
