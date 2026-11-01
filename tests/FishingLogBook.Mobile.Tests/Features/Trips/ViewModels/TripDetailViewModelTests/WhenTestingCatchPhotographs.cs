using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Catch.Offline.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Models;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.TripDetailViewModelTests;

public sealed class WhenTestingCatchPhotographs : BaseTripDetailViewModelTest
{
    private static readonly Guid CatchId = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    private static readonly string RelativePath = $"{ViewerId:N}/photos/pike.jpg";

    private static TripCatchSummaryDto ServerCatch(string? url)
    {
        return new TripCatchSummaryDto(CatchId, Now.AddHours(-1)) { SpeciesName = "Pike", CaughtByUserId = ViewerId, RecordedByUserId = ViewerId, PhotographUrl = url };
    }

    private void ServeLocalCopy(LocalCatchLifecycleEnum lifecycle = LocalCatchLifecycleEnum.Saved, Guid? tripId = null)
    {
        MockCatchStore.ListAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new LocalCatchRecordModel
            {
                Id = CatchId, UserId = ViewerId, TripId = tripId, CaughtByUserId = ViewerId, RecordedByUserId = ViewerId, CaughtOn = Now.AddHours(-1),
                SpeciesName = "Pike", Revision = 1, Lifecycle = lifecycle, CreatedOn = Now, UpdatedOn = Now,
                Photographs =
                [
                    new LocalCatchPhotographModel { Id = Guid.NewGuid(), CatchId = CatchId, UserId = ViewerId, SortOrder = 0, LocalFilePath = RelativePath, ContentType = "image/jpeg", CreatedOn = Now }
                ]
            }
        ]);
    }

    private void ServeFile(bool exists)
    {
        string? resolved = "/data/photos/pike.jpg";
        MockFiles.TryResolveRelativePath(ViewerId, RelativePath, out Arg.Any<string>()).Returns(call =>
        {
            call[2] = exists ? resolved : string.Empty;
            return exists;
        });
    }

    [Fact]
    public async Task ItShouldShowNoPhotographWhenTheCatchHasNeitherAServerLinkNorALocalFile()
    {
        // Arrange
        // Act
        await LoadAsync(Trip(catches: [ServerCatch(null)]));
        // Assert
        Sut.Timeline.Single(row => row.IsCatch).HasPhoto.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldShowTheServerPhotographWhenThereIsNoLocalCopy()
    {
        // Arrange
        // Act
        await LoadAsync(Trip(catches: [ServerCatch("https://img/pike.jpg")]));
        // Assert
        Sut.Timeline.Single(row => row.IsCatch).PhotoSource.Should().Be("https://img/pike.jpg");
    }

    [Fact]
    public async Task ItShouldPreferTheLocalFileOverTheServerLinkSoAJustRecordedCatchShowsBeforeItIsUploaded()
    {
        // Arrange
        ServeLocalCopy();
        ServeFile(exists: true);
        // Act
        await LoadAsync(Trip(catches: [ServerCatch("https://img/pike.jpg")]));
        // Assert
        Sut.Timeline.Single(row => row.IsCatch).PhotoSource.Should().Be("/data/photos/pike.jpg");
    }

    [Fact]
    public async Task ItShouldUseTheLocalFileEvenWhenTheLocalCopyIsStillADraftOrNotOnThisTrip()
    {
        // Arrange
        ServeLocalCopy(LocalCatchLifecycleEnum.Draft, tripId: Guid.NewGuid());
        ServeFile(exists: true);
        // Act
        await LoadAsync(Trip(catches: [ServerCatch(null)]));
        // Assert
        Sut.Timeline.Single(row => row.IsCatch).PhotoSource.Should().Be("/data/photos/pike.jpg");
    }

    [Fact]
    public async Task ItShouldFallBackToTheServerLinkWhenTheLocalFileIsGone()
    {
        // Arrange
        ServeLocalCopy();
        ServeFile(exists: false);
        // Act
        await LoadAsync(Trip(catches: [ServerCatch("https://img/pike.jpg")]));
        // Assert
        Sut.Timeline.Single(row => row.IsCatch).PhotoSource.Should().Be("https://img/pike.jpg");
    }

    [Fact]
    public async Task ItShouldShowNoPhotographWhenTheLocalFileIsGoneAndThereIsNoServerLink()
    {
        // Arrange
        ServeLocalCopy();
        ServeFile(exists: false);
        // Act
        await LoadAsync(Trip(catches: [ServerCatch(null)]));
        // Assert
        Sut.Timeline.Single(row => row.IsCatch).HasPhoto.Should().BeFalse();
    }
}
