using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Catch.Clients;
using FishingLogBook.Mobile.Features.Catch.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Stores;
using FishingLogBook.Mobile.Features.Catch.Services;
using FishingLogBook.Mobile.Features.Photographs.Clients;
using FishingLogBook.Mobile.Features.Photographs.Models;
using FishingLogBook.Mobile.Features.Photographs.Providers;
using FishingLogBook.Mobile.Features.Photographs.Storage.Stores;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.IO.Adapters;
using FishingLogBook.Mobile.Tests.Features.Catch.Offline.Stores.SqliteCatchStoreTests;
using FishingLogBook.Shared.Dtos;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Catch.Services.CatchWorkingCopyServiceTests;

public sealed class WhenTestingEnsure : BaseSqliteCatchStoreTest
{
    private readonly ICatchRemoteClient MockRemote = Substitute.For<ICatchRemoteClient>();
    private readonly IPhotographDownloadClient MockDownload = Substitute.For<IPhotographDownloadClient>();
    private readonly IConnectivityService MockConnectivity = Substitute.For<IConnectivityService>();
    private readonly IPhotographRootPathProvider MockRoot = Substitute.For<IPhotographRootPathProvider>();
    private readonly SqliteLocalCatchCleanupStore _cleanup;
    private readonly PhotographFileStore _files;
    private readonly CatchWorkingCopyService _service;
    private readonly CatchViewDto _view;

    public WhenTestingEnsure()
    {
        MockConnectivity.IsOnline.Returns(true);
        MockRoot.GetPhotographRootDirectory().Returns(Path.Combine(Path.GetDirectoryName(DatabasePath)!, "photographs"));
        _files = new PhotographFileStore(new FileSystemAdapter(), MockRoot, NullLogger<PhotographFileStore>.Instance);
        _cleanup = new SqliteLocalCatchCleanupStore(ConnectionFactory, Database);
        _service = new CatchWorkingCopyService(Sut, MockRemote, MockDownload, _files,
            new SqliteCatchWorkingCopyStore(Database), MockConnectivity, MockSessionCoordinator,
            new ActiveCatchEditRegistry(), _cleanup, TimeProvider.System, NullLogger<CatchWorkingCopyService>.Instance);
        _view = new CatchViewDto(Guid.NewGuid(), OwnerUserId, DateTimeOffset.Parse("2026-10-30T12:00:00Z"))
        {
            RecordedByUserId = OwnerUserId,
            SpeciesName = "Pike",
            Notes = "Server notes",
            Weight = 2.5m,
            Photographs = [new(Guid.NewGuid(), "image/jpeg", "https://photos.example/one"), new(Guid.NewGuid(), "image/jpeg", "https://photos.example/two")]
        };
        MockRemote.GetAsync(_view.Id, Arg.Any<CancellationToken>()).Returns(ApiResultModel<CatchViewDto>.Succeeded(_view));
        MockDownload.DownloadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(PhotographDownloadResultModel.Downloaded([1, 2, 3]));
    }

    [Fact]
    public async Task ItShouldHydrateDurablePhotosWithOriginalIdentitiesAndNoOutboxWork()
    {
        // Act
        var result = await _service.EnsureAsync(_view.Id, CancellationToken.None);

        // Assert
        result.Should().Be(WorkingCopyOutcomeEnum.Ready);
        var record = (await RecreateStoreAgainstSameDatabase().GetAsync(_view.Id, CancellationToken.None))!;
        record.Notes.Should().Be(_view.Notes);
        record.Weight.Should().Be(2.5);
        record.Photographs.Select(photo => photo.Id).Should().Equal(_view.Photographs.Select(photo => photo.Id));
        foreach (var photo in record.Photographs)
        {
            File.ReadAllBytes(_files.GetAbsolutePath(OwnerUserId, photo.LocalFilePath!)).Should().Equal([1, 2, 3]);
        }
        (await _cleanup.ListPendingFileDeletionsAsync(OwnerUserId, CancellationToken.None)).Should().BeEmpty();
        var status = new SqliteCatchSyncStatusStore(ConnectionFactory, Database);
        (await status.ListAsync(OwnerUserId, CancellationToken.None))[_view.Id].MetadataSynced.Should().BeTrue();
        await using var connection = await ConnectionFactory.OpenAsync(CancellationToken.None);
        await using var query = connection.CreateCommand();
        query.CommandText = "SELECT COUNT(*) FROM outboxoperations;";
        Convert.ToInt32(await query.ExecuteScalarAsync()).Should().Be(0);
        await MockDownload.Received(1).DownloadAsync("https://photos.example/one", Arg.Any<CancellationToken>());
        await MockDownload.Received(1).DownloadAsync("https://photos.example/two", Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(LocationDefaults.Private)]
    [InlineData(LocationDefaults.Public)]
    [InlineData(LocationDefaults.Approximate)]
    [InlineData(LocationDefaults.FishingVenueOnly)]
    public async Task ItShouldPreserveTheOwnersExactLocationAndVisibilityInTheDurableWorkingCopy(string visibility)
    {
        // Arrange
        var location = new CatchLocationExposureDto
        {
            Mode = LocationDefaults.ExposureExact,
            Visibility = visibility,
            Latitude = 53.3,
            Longitude = -6.2,
            AccuracyMetres = 12,
            CapturedOn = _view.CaughtOn,
            Source = LocationDefaults.PhotoMetadata
        };
        MockRemote.GetAsync(_view.Id, Arg.Any<CancellationToken>()).Returns(ApiResultModel<CatchViewDto>.Succeeded(_view with { Location = location, PlaceName = "Lough Corrib" }));
        // Act
        var outcome = await _service.EnsureAsync(_view.Id, CancellationToken.None);
        // Assert
        outcome.Should().Be(WorkingCopyOutcomeEnum.Ready);
        var record = (await Sut.GetAsync(_view.Id, CancellationToken.None))!;
        record.PlaceName.Should().Be("Lough Corrib");
        record.Location!.Latitude.Should().Be(location.Latitude);
        record.Location.Longitude.Should().Be(location.Longitude);
        record.Location.AccuracyMetres.Should().Be(location.AccuracyMetres);
        record.Location.CapturedOn.Should().Be(location.CapturedOn);
        record.Location.Source.Should().Be(location.Source);
        record.Location.Visibility.Should().Be(visibility);
        record.Location.ConsentVersion.Should().Be(LocationDefaults.ConsentVersion);
    }

    [Fact]
    public async Task ItShouldKeepAnExistingPendingRecordWithoutRequestingTheServer()
    {
        // Arrange
        await Sut.SaveAsync(CreateSaveArgs(_view.Id), CancellationToken.None);
        // Act
        var result = await _service.EnsureAsync(_view.Id, CancellationToken.None);
        // Assert
        result.Should().Be(WorkingCopyOutcomeEnum.Ready);
        await MockRemote.DidNotReceive().GetAsync(_view.Id, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(ApiOutcomeEnum.NotFound, WorkingCopyOutcomeEnum.NotFound)]
    [InlineData(ApiOutcomeEnum.SessionExpired, WorkingCopyOutcomeEnum.SessionExpired)]
    [InlineData(ApiOutcomeEnum.Unavailable, WorkingCopyOutcomeEnum.NeedsConnection)]
    public async Task ItShouldBlockEditingWhenTheServerCannotSupplyTheCatch(ApiOutcomeEnum api, WorkingCopyOutcomeEnum expected)
    {
        // Arrange
        MockRemote.GetAsync(_view.Id, Arg.Any<CancellationToken>()).Returns(ApiResultModel<CatchViewDto>.Failed(api));
        // Act
        var result = await _service.EnsureAsync(_view.Id, CancellationToken.None);
        // Assert
        result.Should().Be(expected);
        await MockDownload.DidNotReceive().DownloadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        (await Sut.ListAsync(CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldBlockOfflineEditingWithoutDownloading()
    {
        // Arrange
        MockConnectivity.IsOnline.Returns(false);
        // Act
        var result = await _service.EnsureAsync(_view.Id, CancellationToken.None);
        // Assert
        result.Should().Be(WorkingCopyOutcomeEnum.NeedsConnection);
        await MockRemote.DidNotReceive().GetAsync(_view.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotHydrateAnotherRecordersCatch()
    {
        // Arrange
        MockRemote.GetAsync(_view.Id, Arg.Any<CancellationToken>()).Returns(ApiResultModel<CatchViewDto>.Succeeded(_view with { RecordedByUserId = OtherUserId }));
        // Act
        var result = await _service.EnsureAsync(_view.Id, CancellationToken.None);
        // Assert
        result.Should().Be(WorkingCopyOutcomeEnum.NotEditable);
        await MockDownload.DidNotReceive().DownloadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ItShouldBlockUnsupportedTripOrOtherAnglerProvenanceBeforeDownloading(bool trip)
    {
        // Arrange
        var view = trip ? _view with { TripId = Guid.NewGuid() } : _view with { CaughtByUserId = OtherUserId };
        MockRemote.GetAsync(_view.Id, Arg.Any<CancellationToken>()).Returns(ApiResultModel<CatchViewDto>.Succeeded(view));
        // Act
        var result = await _service.EnsureAsync(_view.Id, CancellationToken.None);
        // Assert
        result.Should().Be(WorkingCopyOutcomeEnum.NotEditable);
        await MockDownload.DidNotReceive().DownloadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        (await Sut.ListAsync(CancellationToken.None)).Should().BeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ItShouldNotCreateAnIncompletePhotoWorkingCopy(bool noPhotos)
    {
        // Arrange
        var view = _view with { Photographs = noPhotos ? [] : [_view.Photographs[0] with { Url = null }] };
        MockRemote.GetAsync(_view.Id, Arg.Any<CancellationToken>()).Returns(ApiResultModel<CatchViewDto>.Succeeded(view));
        // Act
        var result = await _service.EnsureAsync(_view.Id, CancellationToken.None);
        // Assert
        result.Should().Be(WorkingCopyOutcomeEnum.PhotosUnavailable);
        (await Sut.ListAsync(CancellationToken.None)).Should().BeEmpty();
        await MockDownload.DidNotReceive().DownloadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ItShouldTrackPreparedFilesForRecoveryAfterInterruptedDownload(bool cancelled)
    {
        // Arrange
        MockDownload.DownloadAsync("https://photos.example/two", Arg.Any<CancellationToken>())
            .Returns<Task<PhotographDownloadResultModel>>(_ => throw (cancelled ? new OperationCanceledException() : new IOException("Interrupted")));
        // Act
        if (cancelled)
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _service.EnsureAsync(_view.Id, CancellationToken.None));
        }
        else
        {
            (await _service.EnsureAsync(_view.Id, CancellationToken.None)).Should().Be(WorkingCopyOutcomeEnum.PhotosUnavailable);
        }
        // Assert
        (await Sut.ListAsync(CancellationToken.None)).Should().BeEmpty();
        var restartedCleanup = new SqliteLocalCatchCleanupStore(ConnectionFactory, Database);
        var pending = (await restartedCleanup.ListPendingFileDeletionsAsync(OwnerUserId, CancellationToken.None)).Should().ContainSingle().Subject;
        File.Exists(_files.GetAbsolutePath(OwnerUserId, pending.RelativePath)).Should().BeTrue();
        await restartedCleanup.TryDeleteFileAsync(OwnerUserId, pending.Id, pending.RelativePath,
            () => _files.TryDelete(OwnerUserId, pending.RelativePath), CancellationToken.None);
        File.Exists(_files.GetAbsolutePath(OwnerUserId, pending.RelativePath)).Should().BeFalse();
        (await restartedCleanup.ListPendingFileDeletionsAsync(OwnerUserId, CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldNotCommitAfterAnAccountSwitchDuringDownload()
    {
        // Arrange
        MockDownload.DownloadAsync("https://photos.example/two", Arg.Any<CancellationToken>()).Returns(_ =>
        {
            MockSessionCoordinator.IsCurrent(OwnerLease).Returns(false);
            return PhotographDownloadResultModel.Downloaded([4, 5]);
        });
        // Act
        var result = await _service.EnsureAsync(_view.Id, CancellationToken.None);
        // Assert
        result.Should().Be(WorkingCopyOutcomeEnum.SessionExpired);
        (await Sut.GetForOwnerAsync(OwnerUserId, _view.Id, CancellationToken.None)).Should().BeNull();
        (await CreateStoreForUser(OtherUserId).ListAsync(CancellationToken.None)).Should().BeEmpty();
    }
}
