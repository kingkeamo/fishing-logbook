using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Import.Services;
using FishingLogBook.Mobile.IO.Adapters;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Import.Services.ImportPhotoBlobRegistryServiceTests;

public sealed class WhenTestingRecoverAbandonedFiles
{
    private const string Root = "/cache/import-photos";

    [Fact]
    public void ItShouldDeleteAbandonedImportFilesOnConstruction()
    {
        // Arrange
        var files = Substitute.For<IFileSystemAdapter>();
        var orphan = Path.Combine(Root, "abandoned");
        files.DirectoryExists(Root).Returns(true);
        files.EnumerateFiles(Root, "*").Returns([orphan]);
        files.FileExists(orphan).Returns(true);

        // Act
        _ = new ImportPhotoBlobRegistryService(files, Root);

        // Assert
        files.Received(1).DeleteFile(orphan);
    }

    [Fact]
    public async Task ItShouldKeepTrackedImportFilesAndDeleteOnlyOrphans()
    {
        // Arrange
        var files = Substitute.For<IFileSystemAdapter>();
        files.Create(Arg.Any<string>()).Returns(_ => new MemoryStream());
        var sut = new ImportPhotoBlobRegistryService(files, Root);
        var registration = await sut.RegisterAsync([1, 2, 3], "image/jpeg", CancellationToken.None);
        var tracked = Path.Combine(Root, registration.Token);
        var orphan = Path.Combine(Root, "abandoned");
        files.DirectoryExists(Root).Returns(true);
        files.EnumerateFiles(Root, "*").Returns([tracked, orphan]);
        files.FileExists(tracked).Returns(true);
        files.FileExists(orphan).Returns(true);

        // Act
        sut.RecoverAbandonedFiles();

        // Assert
        files.DidNotReceive().DeleteFile(tracked);
        files.Received().DeleteFile(orphan);
    }

    [Fact]
    public void ItShouldNotEnumerateFilesOutsideTheImportCache()
    {
        // Arrange
        var files = Substitute.For<IFileSystemAdapter>();
        files.DirectoryExists(Root).Returns(true);
        files.EnumerateFiles(Root, "*").Returns(Array.Empty<string>());

        // Act
        _ = new ImportPhotoBlobRegistryService(files, Root);

        // Assert
        files.Received().EnumerateFiles(Root, "*");
        files.DidNotReceive().EnumerateFiles(Arg.Is<string>(path => path != Root), Arg.Any<string>());
        files.DidNotReceive().DeleteFile(Arg.Any<string>());
    }

    [Fact]
    public async Task ItShouldClearTrackedFilesAndSweepLeftovers()
    {
        // Arrange
        var files = Substitute.For<IFileSystemAdapter>();
        files.Create(Arg.Any<string>()).Returns(_ => new MemoryStream());
        var sut = new ImportPhotoBlobRegistryService(files, Root);
        var registration = await sut.RegisterAsync([1, 2, 3], "image/jpeg", CancellationToken.None);
        var tracked = Path.Combine(Root, registration.Token);
        var leftover = Path.Combine(Root, "leftover");
        files.FileExists(tracked).Returns(true);
        files.DirectoryExists(Root).Returns(true);
        files.EnumerateFiles(Root, "*").Returns([leftover]);
        files.FileExists(leftover).Returns(true);

        // Act
        await sut.ClearAsync(CancellationToken.None);

        // Assert
        files.Received().DeleteFile(tracked);
        files.Received().DeleteFile(leftover);
    }
}
