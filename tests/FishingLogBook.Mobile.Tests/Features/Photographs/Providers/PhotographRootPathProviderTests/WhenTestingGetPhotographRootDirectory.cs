using AwesomeAssertions;
using FishingLogBook.Mobile.Common.Offline.Sqlite;
using FishingLogBook.Mobile.Features.Photographs.Providers;
using FishingLogBook.Mobile.IO.Adapters;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Photographs.Providers.PhotographRootPathProviderTests;

public class WhenTestingGetPhotographRootDirectory
{
    private readonly IFileSystemAdapter _mockFileSystem = Substitute.For<IFileSystemAdapter>();
    private readonly ILocalDatabasePathProvider _mockDatabasePath = Substitute.For<ILocalDatabasePathProvider>();

    [Fact]
    public void ItShouldUseTheAppDataDirectoryWhenThereIsNoDatabasePathProvider()
    {
        // Arrange
        var sut = new PhotographRootPathProvider(_mockFileSystem, () => Path.Combine("data", "app"));

        // Act
        var root = sut.GetPhotographRootDirectory();

        // Assert
        root.Should().Be(Path.Combine("data", "app", "photographs"));
        _mockFileSystem.Received(1).CreateDirectory(root);
    }

    [Fact]
    public void ItShouldKeepPhotographsNextToTheAccountDatabase()
    {
        // Arrange
        var databasePath = Path.Combine("data", "accounts", "user-1", "catches.db");
        _mockDatabasePath.GetDatabasePath().Returns(databasePath);
        var sut = new PhotographRootPathProvider(_mockFileSystem, () => Path.Combine("data", "app"), _mockDatabasePath);

        // Act
        var root = sut.GetPhotographRootDirectory();

        // Assert
        root.Should().Be(Path.Combine("data", "accounts", "user-1", "photographs"));
        _mockFileSystem.Received(1).CreateDirectory(root);
        _mockFileSystem.DidNotReceive().CreateDirectory(Path.Combine("data", "app", "photographs"));
    }

    [Fact]
    public void ItShouldFallBackToTheAppDataDirectoryWhenTheDatabasePathHasNoDirectory()
    {
        // Arrange
        _mockDatabasePath.GetDatabasePath().Returns(Path.GetPathRoot(Path.GetFullPath("."))!);
        var sut = new PhotographRootPathProvider(_mockFileSystem, () => Path.Combine("data", "app"), _mockDatabasePath);

        // Act
        var root = sut.GetPhotographRootDirectory();

        // Assert
        root.Should().Be(Path.Combine("data", "app", "photographs"));
        _mockFileSystem.Received(1).CreateDirectory(root);
    }
}
