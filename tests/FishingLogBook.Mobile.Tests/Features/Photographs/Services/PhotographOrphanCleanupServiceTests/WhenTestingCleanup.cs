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
    public async Task ShouldDeleteUnreferencedFilesAndKeepSqliteReferencedFiles()
    {
        var photoRoot = Path.Combine(Path.GetDirectoryName(DatabasePath)!, "photographs");
        Directory.CreateDirectory(photoRoot);
        var referencedId = Guid.NewGuid();
        var orphanId = Guid.NewGuid();
        var referencedRelative = $"{OwnerUserId:N}/{referencedId:N}.jpg";
        var referencedAbsolute = Path.Combine(photoRoot, OwnerUserId.ToString("N"), $"{referencedId:N}.jpg");
        var orphanAbsolute = Path.Combine(photoRoot, OwnerUserId.ToString("N"), $"{orphanId:N}.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(referencedAbsolute)!);
        await File.WriteAllBytesAsync(referencedAbsolute, [0xFF, 0xD8, 0xFF, 0xD9]);
        await File.WriteAllBytesAsync(orphanAbsolute, [0xFF, 0xD8, 0xFF, 0xD9]);

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

        var referenceStore = new SqlitePhotographReferenceStore(ConnectionFactory, Database);
        var sut = new PhotographOrphanCleanupService(
            new FileSystemAdapter(),
            new FixedRoot(photoRoot),
            referenceStore,
            Database,
            NullLogger<PhotographOrphanCleanupService>.Instance);

        await sut.CleanupAllAccountsAsync(CancellationToken.None);

        File.Exists(referencedAbsolute).Should().BeTrue();
        File.Exists(orphanAbsolute).Should().BeFalse();
    }

    private sealed class FixedRoot(string root) : IPhotographRootPathProvider
    {
        public string GetPhotographRootDirectory() => root;
    }
}
