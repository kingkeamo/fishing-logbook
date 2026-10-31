using System.Globalization;
using AwesomeAssertions;
using FishingLogBook.Mobile.Common.Offline.Services;
using FishingLogBook.Mobile.Common.Offline.Sqlite;
using FishingLogBook.Mobile.Common.Offline.Stores;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Catch.Mappers;
using FishingLogBook.Mobile.Features.Catch.Offline.Stores;
using FishingLogBook.Mobile.Features.Catch.ViewModels;
using FishingLogBook.Mobile.Features.Photographs.Offline.Stores;
using FishingLogBook.Mobile.Features.Photographs.Providers;
using FishingLogBook.Mobile.Features.Photographs.Services;
using FishingLogBook.Mobile.Features.Photographs.Storage.Stores;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.IO.Adapters;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Shared.Measurements;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Common.Offline.Sqlite.SqliteDatabaseTests;

public sealed class WhenTestingDependencyInjection : IDisposable
{
    private readonly string _directory;
    private readonly string _databasePath;
    private readonly string _photoRoot;

    public WhenTestingDependencyInjection()
    {
        _directory = Path.Combine(Path.GetTempPath(), "flb-sqlite-di-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _databasePath = Path.Combine(_directory, "local-logbook.db");
        _photoRoot = Path.Combine(_directory, "photographs");
        Directory.CreateDirectory(_photoRoot);
    }

    [Fact]
    public async Task ShouldResolveCatchListGraphWhenSqliteObserversAreRegisteredLikeMauiProgram()
    {
        var services = CreateMauiStyleSqliteRegistrations();
        var session = Substitute.For<IAccountSessionCoordinator>();
        session.Capture().Returns(new AccountSessionLeaseModel(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            1,
            CancellationToken.None));
        services.AddSingleton(session);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IOutboxStore, SqliteOutboxStore>();
        services.AddSingleton<IOutboxChangeNotifier, OutboxChangeNotifier>();
        services.AddSingleton(Substitute.For<ICatchSyncStatusStore>());
        services.AddSingleton(Microsoft.Extensions.Logging.Abstractions.NullLogger<CatchListViewModel>.Instance);
        services.AddSingleton<ICatchStore, SqliteCatchStore>();
        services.AddSingleton(Substitute.For<FishingLogBook.Mobile.Features.Catch.Clients.ICatchRemoteClient>());
        services.AddSingleton(Substitute.For<FishingLogBook.Mobile.Features.Profile.Services.IConnectivityService>());
        services.AddSingleton<FishingLogBook.Mobile.Features.Catch.Services.ICatchHistoryService, FishingLogBook.Mobile.Features.Catch.Services.CatchHistoryService>();
        services.AddSingleton(new UiTextService(CultureInfo.GetCultureInfo("en-GB")));
        services.AddSingleton(Substitute.For<IPhotographFileStore>());
        services.AddSingleton<CatchPresentationMapper>();
        var units = Substitute.For<IAccountUnitsService>();
        units.GetAsync(Arg.Any<CancellationToken>()).Returns(MeasurementUnitsModel.Default);
        services.AddSingleton(units);
        services.AddSingleton<IMeasurementService, MeasurementService>();
        services.AddSingleton<CatchListViewModel>();

        await using var provider = services.BuildServiceProvider();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var resolve = Task.Run(
            () =>
            {
                var database = provider.GetRequiredService<ISqliteDatabase>();
                var cleanup = provider.GetRequiredService<PhotographOrphanCleanupService>();
                var catchStore = provider.GetRequiredService<ICatchStore>();
                var list = provider.GetRequiredService<CatchListViewModel>();
                return (database, cleanup, catchStore, list);
            },
            timeout.Token);

        var resolved = await resolve.WaitAsync(timeout.Token);
        resolved.database.Should().BeOfType<SqliteDatabase>();
        resolved.cleanup.Should().NotBeNull();
        resolved.catchStore.Should().BeOfType<SqliteCatchStore>();
        resolved.list.Should().NotBeNull();

        await resolved.database.EnsureInitializedAsync(timeout.Token).WaitAsync(TimeSpan.FromSeconds(5));
        File.Exists(_databasePath).Should().BeTrue();
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private ServiceCollection CreateMauiStyleSqliteRegistrations()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IFileSystemAdapter, FileSystemAdapter>();
        services.AddSingleton<IPhotographRootPathProvider>(_ => new FixedRoot(_photoRoot));
        services.AddSingleton<ISqliteConnectionFactory>(_ => new SqliteConnectionFactory(_databasePath));
        services.AddSingleton<ISqliteMigrator, SqliteMigrator>();
        services.AddSingleton<PhotographOrphanCleanupService>();
        services.AddSingleton<IPhotographOrphanCleanupService>(sp =>
            sp.GetRequiredService<PhotographOrphanCleanupService>());
        services.AddSingleton<ILocalDatabaseInitializedObserver>(sp =>
            sp.GetRequiredService<PhotographOrphanCleanupService>());
        services.AddSingleton<ISqliteDatabase>(sp => new SqliteDatabase(
            sp.GetRequiredService<ISqliteConnectionFactory>(),
            sp.GetRequiredService<ISqliteMigrator>(),
            sp.GetRequiredService<ILogger<SqliteDatabase>>(),
            () => sp.GetServices<ILocalDatabaseInitializedObserver>()));
        services.AddSingleton<IPhotographReferenceStore, SqlitePhotographReferenceStore>();
        return services;
    }

    private sealed class FixedRoot(string root) : IPhotographRootPathProvider
    {
        public string GetPhotographRootDirectory() => root;
    }
}
