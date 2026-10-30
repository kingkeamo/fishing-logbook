using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Catch.Offline.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Models;
using FishingLogBook.Mobile.Features.Photographs.Offline.Stores;
using FishingLogBook.Mobile.Features.Photographs.Providers;
using FishingLogBook.Mobile.Features.Photographs.Services;
using FishingLogBook.Mobile.IO.Adapters;
using FishingLogBook.Mobile.Tests.Features.Catch.Offline.Stores.SqliteCatchStoreTests;
using FishingLogBook.Shared.Constants;
using Microsoft.Extensions.Logging.Abstractions;

namespace FishingLogBook.Mobile.Tests.Features.Photographs.Services.PhotographOrphanCleanupServiceTests;

public class WhenTestingCleanup : BaseSqliteCatchStoreTest
{
    [Fact]
    public async Task ShouldRetainUnreferencedDurableFilesAndKeepSqliteReferencedFiles()
    {
        // Arrange
        var photoRoot = Path.Combine(Path.GetDirectoryName(DatabasePath)!, "photographs");
        Directory.CreateDirectory(photoRoot);
        var referencedId = Guid.NewGuid();
        var preparedNotSavedId = Guid.NewGuid();
        var referencedRelative = $"{OwnerUserId:N}/{referencedId:N}.jpg";
        var referencedAbsolute = Path.Combine(photoRoot, OwnerUserId.ToString("N"), $"{referencedId:N}.jpg");
        var preparedAbsolute = Path.Combine(photoRoot, OwnerUserId.ToString("N"), $"{preparedNotSavedId:N}.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(referencedAbsolute)!);
        await File.WriteAllBytesAsync(referencedAbsolute, [0xFF, 0xD8, 0xFF, 0xD9]);
        await File.WriteAllBytesAsync(preparedAbsolute, [0xFF, 0xD8, 0xFF, 0xD9]);

        var catchId = Guid.NewGuid();
        await Sut.SaveAsync(new LocalCatchSaveArgs
        {
            Id = catchId,
            CaughtOn = DateTimeOffset.Parse("2026-05-01T12:00:00Z"),
            SpeciesName = "Brown trout",
            Weight = 1.2,
            Length = 42,
            Method = "Fly",
            Lifecycle = LocalCatchLifecycleEnum.Saved,
            Photographs =
            [
                new LocalCatchPhotographSaveArgs
                {
                    Id = referencedId,
                    SortOrder = 0,
                    LocalFilePath = referencedRelative,
                    ContentType = PhotographContentTypeConstants.Jpeg
                }
            ]
        }, CancellationToken.None);

        var sut = CreateCleanupService(photoRoot);

        // Act
        await sut.CleanupAllAccountsAsync(CancellationToken.None);

        // Assert
        File.Exists(referencedAbsolute).Should().BeTrue();
        File.Exists(preparedAbsolute).Should().BeTrue();
    }

    [Fact]
    public async Task ShouldDeleteAbandonedTempFilesOnly()
    {
        // Arrange
        var photoRoot = Path.Combine(Path.GetDirectoryName(DatabasePath)!, "photographs");
        var accountDirectory = Path.Combine(photoRoot, OwnerUserId.ToString("N"));
        Directory.CreateDirectory(accountDirectory);
        var durableAbsolute = Path.Combine(accountDirectory, $"{Guid.NewGuid():N}.jpg");
        var tempAbsolute = Path.Combine(accountDirectory, $".{Guid.NewGuid():N}.tmp");
        await File.WriteAllBytesAsync(durableAbsolute, [0xFF, 0xD8, 0xFF, 0xD9]);
        await File.WriteAllBytesAsync(tempAbsolute, [0x00, 0x01]);
        var sut = CreateCleanupService(photoRoot);

        // Act
        await sut.CleanupAllAccountsAsync(CancellationToken.None);

        // Assert
        File.Exists(durableAbsolute).Should().BeTrue();
        File.Exists(tempAbsolute).Should().BeFalse();
    }

    [Fact]
    public async Task ShouldRetainPreparedPhotographAcrossRestartBeforeSqliteSave()
    {
        // Arrange
        var photoRoot = Path.Combine(Path.GetDirectoryName(DatabasePath)!, "photographs");
        var preparedId = Guid.NewGuid();
        var preparedAbsolute = Path.Combine(photoRoot, OwnerUserId.ToString("N"), $"{preparedId:N}.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(preparedAbsolute)!);
        await File.WriteAllBytesAsync(preparedAbsolute, [0xFF, 0xD8, 0xFF, 0xD9]);
        var cleanup = CreateCleanupService(photoRoot);
        var database = new FishingLogBook.Mobile.Common.Offline.Sqlite.SqliteDatabase(
            ConnectionFactory,
            Migrator,
            NullLogger<FishingLogBook.Mobile.Common.Offline.Sqlite.SqliteDatabase>.Instance,
            [cleanup]);

        // Act
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await database.EnsureInitializedAsync(timeout.Token);

        // Assert
        File.Exists(preparedAbsolute).Should().BeTrue();
    }

    private PhotographOrphanCleanupService CreateCleanupService(string photoRoot)
    {
        var referenceStore = new SqlitePhotographReferenceStore(ConnectionFactory, Database);
        return new PhotographOrphanCleanupService(
            new FileSystemAdapter(),
            new FixedRoot(photoRoot),
            referenceStore,
            Database,
            NullLogger<PhotographOrphanCleanupService>.Instance);
    }

    private sealed class FixedRoot(string root) : IPhotographRootPathProvider
    {
        public string GetPhotographRootDirectory() => root;
    }
}
