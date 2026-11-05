using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Photographs.Providers;
using FishingLogBook.Mobile.Features.Photographs.Storage.Stores;
using FishingLogBook.Mobile.IO.Adapters;
using FishingLogBook.Mobile.Tests.Support;
using FishingLogBook.Shared.Constants;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Mobile.Tests.Features.Photographs.Storage.Stores.PhotographFileStoreTests;

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

    [Theory]
    [InlineData("root")]
    [InlineData("directory")]
    [InlineData("write")]
    [InlineData("move")]
    public async Task ShouldReturnNoSavedPathAndRemoveTemporaryFilesOnStorageFailure(string stage)
    {
        // Arrange
        var mockFileSystem = Substitute.For<IFileSystemAdapter>();
        mockFileSystem.When(fs => fs.CreateDirectory(Arg.Any<string>()))
            .Do(call => Directory.CreateDirectory(call.Arg<string>()));
        mockFileSystem.Create(Arg.Any<string>()).Returns(call => File.Create(call.Arg<string>()));
        mockFileSystem.FileExists(Arg.Any<string>()).Returns(call => File.Exists(call.Arg<string>()));
        mockFileSystem.When(fs => fs.DeleteFile(Arg.Any<string>())).Do(call => File.Delete(call.Arg<string>()));
        var failure = new IOException("PRIVATE-PATH");
        if (stage == "directory")
        {
            mockFileSystem.When(fs => fs.CreateDirectory(Arg.Any<string>())).Do(_ => throw failure);
        }
        else if (stage == "write")
        {
            mockFileSystem.Create(Arg.Any<string>()).Throws(failure);
        }
        else
        {
            mockFileSystem.When(fs => fs.MoveFile(Arg.Any<string>(), Arg.Any<string>(), true)).Do(_ => throw failure);
        }

        var userId = Guid.NewGuid();
        var photographId = Guid.NewGuid();
        IPhotographRootPathProvider rootProvider = new FixedPhotographRootPathProvider(_root);
        if (stage == "root")
        {
            var mockRootProvider = Substitute.For<IPhotographRootPathProvider>();
            mockRootProvider.GetPhotographRootDirectory().Throws(failure);
            rootProvider = mockRootProvider;
        }

        var logger = new CapturingLogger<PhotographFileStore>();
        var sut = new PhotographFileStore(mockFileSystem, rootProvider, logger);

        // Act
        var relative = await sut.SaveAsync(userId, photographId, "image/jpeg", MinimalJpeg(), CancellationToken.None);

        // Assert
        relative.Should().BeNull();
        logger.Entries.Should().ContainSingle(entry => ReferenceEquals(entry.Exception, failure));
        Directory.GetFiles(_root, "*", SearchOption.AllDirectories).Should().BeEmpty();
        if (stage == "root")
        {
            mockFileSystem.DidNotReceiveWithAnyArgs().CreateDirectory(default!);
        }
        else
        {
            mockFileSystem.Received(1).CreateDirectory(Path.Combine(_root, userId.ToString("N")));
        }
        if (stage == "move")
        {
            var tempPath = Path.Combine(_root, userId.ToString("N"), $".{photographId:N}.tmp");
            mockFileSystem.Received(1).MoveFile(tempPath, Path.Combine(_root, userId.ToString("N"), $"{photographId:N}.jpg"), true);
            mockFileSystem.Received(1).DeleteFile(tempPath);
        }
        else
        {
            mockFileSystem.DidNotReceiveWithAnyArgs().MoveFile(default!, default!, default);
        }
    }

    [Fact]
    public async Task ShouldRemoveAnInterruptedTemporaryFileAndPropagateCancellation()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var mockFileSystem = Substitute.For<IFileSystemAdapter>();
        mockFileSystem.When(fs => fs.CreateDirectory(Arg.Any<string>()))
            .Do(call => Directory.CreateDirectory(call.Arg<string>()));
        mockFileSystem.Create(Arg.Any<string>()).Returns(call =>
        {
            var stream = File.Create(call.Arg<string>());
            cancellation.Cancel();
            return stream;
        });
        mockFileSystem.FileExists(Arg.Any<string>()).Returns(call => File.Exists(call.Arg<string>()));
        mockFileSystem.When(fs => fs.DeleteFile(Arg.Any<string>())).Do(call => File.Delete(call.Arg<string>()));
        var userId = Guid.NewGuid();
        var photographId = Guid.NewGuid();
        var tempPath = Path.Combine(_root, userId.ToString("N"), $".{photographId:N}.tmp");
        var sut = new PhotographFileStore(mockFileSystem, new FixedPhotographRootPathProvider(_root), NullLogger<PhotographFileStore>.Instance);

        // Act
        var act = () => sut.SaveAsync(userId, photographId, "image/jpeg", MinimalJpeg(), cancellation.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        Directory.GetFiles(_root, "*", SearchOption.AllDirectories).Should().BeEmpty();
        mockFileSystem.Received(1).Create(tempPath);
        mockFileSystem.Received(1).DeleteFile(tempPath);
        mockFileSystem.DidNotReceiveWithAnyArgs().MoveFile(default!, default!, default);
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
