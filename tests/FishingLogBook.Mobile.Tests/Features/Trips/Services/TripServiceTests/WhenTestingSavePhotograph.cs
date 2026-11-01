using AwesomeAssertions;
using FishingLogBook.Mobile.Common.Offline.Enums;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.Services.TripServiceTests;

public sealed class WhenTestingSavePhotograph : BaseTripServiceTest
{
    private async Task<LocalTripRecordModel> StartOfflineAsync()
    {
        MockConnectivity.IsOnline.Returns(false);
        return await TripSut.StartAsync("Corrib", null, CancellationToken.None);
    }

    private static TripPhotographViewDto Photograph(Guid? by = null, Guid? id = null)
    {
        return new TripPhotographViewDto(id ?? Guid.NewGuid(), "image/jpeg", Now.AddMinutes(-5), null) { ContributedByUserId = by ?? OwnerUserId };
    }

    [Fact]
    public async Task ItShouldRefuseAPhotographFromSomeoneWhoCannotContributeToTheTrip()
    {
        // Arrange
        var trip = (await StartOfflineAsync()) with { AccessRevoked = true };
        // Act
        var act = () => TripSut.SavePhotographAsync(trip, Photograph(), "photos/a.jpg", deleted: false, CancellationToken.None);
        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ItShouldRefuseAnotherAnglersPhotograph()
    {
        // Arrange
        var trip = await StartOfflineAsync();
        // Act
        var act = () => TripSut.SavePhotographAsync(trip, Photograph(by: OtherUserId), "photos/a.jpg", deleted: false, CancellationToken.None);
        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ItShouldRefuseANewPhotographOnAFinishedTrip()
    {
        // Arrange
        var started = await StartOfflineAsync();
        var trip = started with { Detail = started.Detail with { Trip = started.Detail.Trip with { Status = TripConstants.Completed, EndedOn = Now } } };
        // Act
        var act = () => TripSut.SavePhotographAsync(trip, Photograph(), "photos/a.jpg", deleted: false, CancellationToken.None);
        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ItShouldRejectANewPhotographWithoutAFile()
    {
        // Arrange
        var trip = await StartOfflineAsync();
        // Act
        var act = () => TripSut.SavePhotographAsync(trip, Photograph(), null, deleted: false, CancellationToken.None);
        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ItShouldRejectAnEleventhPhotograph()
    {
        // Arrange
        var started = await StartOfflineAsync();
        var trip = started with { Detail = started.Detail with { Photographs = [.. Enumerable.Range(0, 10).Select(_ => Photograph())] } };
        // Act
        var act = () => TripSut.SavePhotographAsync(trip, Photograph(), "photos/a.jpg", deleted: false, CancellationToken.None);
        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ItShouldStoreThePhotographPendingWithItsFileAndShowItOnTheTripAcrossARestart()
    {
        // Arrange
        var trip = await StartOfflineAsync();
        var photograph = Photograph();
        // Act
        await TripSut.SavePhotographAsync(trip, photograph, "photos/a.jpg", deleted: false, CancellationToken.None);
        // Assert
        (await CreateTripStore().ListContributionsAsync(OwnerUserId, trip.Detail.Trip.Id, CancellationToken.None))
            .Should().ContainSingle(item => item.Id == photograph.Id && item.IsPending && item.RelativeFilePath == "photos/a.jpg" && !item.Deleted);
        (await OutboxStore.ListPendingAsync(OwnerUserId, CancellationToken.None))
            .Should().Contain(operation => operation.AggregateId == photograph.Id && operation.OperationType == OutboxOperationTypeEnum.WriteTripPhotograph);
        (await TripSut.GetAsync(trip.Detail.Trip.Id, CancellationToken.None))!.Detail.Photographs.Should().ContainSingle(item => item.Id == photograph.Id);
    }

    [Fact]
    public async Task ItShouldKeepTheStoredFileWhenThePhotographIsLaterDeletedAndRemoveItFromTheTrip()
    {
        // Arrange
        var trip = await StartOfflineAsync();
        var photograph = Photograph();
        await TripSut.SavePhotographAsync(trip, photograph, "photos/a.jpg", deleted: false, CancellationToken.None);
        // Act
        await TripSut.SavePhotographAsync(trip, photograph, null, deleted: true, CancellationToken.None);
        // Assert
        (await TripSut.GetAsync(trip.Detail.Trip.Id, CancellationToken.None))!.Detail.Photographs.Should().BeEmpty();
        (await TripStore.ListContributionsAsync(OwnerUserId, trip.Detail.Trip.Id, CancellationToken.None))
            .Should().ContainSingle(item => item.Deleted && item.IsPending && item.RelativeFilePath == "photos/a.jpg");
    }

    [Fact]
    public async Task ItShouldAllowDeletingAPhotographFromAFinishedTrip()
    {
        // Arrange
        var started = await StartOfflineAsync();
        var photograph = Photograph();
        var trip = started with { Detail = started.Detail with { Trip = started.Detail.Trip with { Status = TripConstants.Completed, EndedOn = Now } } };
        // Act
        var act = () => TripSut.SavePhotographAsync(trip, photograph, null, deleted: true, CancellationToken.None);
        // Assert
        await act.Should().NotThrowAsync();
    }
}
