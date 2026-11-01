using AwesomeAssertions;
using FishingLogBook.Mobile.Common.Offline.Enums;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.Services.TripServiceTests;

public sealed class WhenTestingUpdate : BaseTripServiceTest
{
    private async Task<LocalTripRecordModel> StartOfflineAsync()
    {
        MockConnectivity.IsOnline.Returns(false);
        return await TripSut.StartAsync("Corrib", "Lough Corrib", CancellationToken.None);
    }

    [Fact]
    public async Task ItShouldRefuseAnEditFromSomeoneWhoCannotContributeToTheTrip()
    {
        // Arrange
        var trip = (await StartOfflineAsync()) with { AccessRevoked = true };
        // Act
        var act = () => TripSut.UpdateAsync(trip, trip.Detail.Trip with { Title = "Changed" }, CancellationToken.None);
        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ItShouldRefuseAnEditFromAParticipantWhoDoesNotOwnTheTrip()
    {
        // Arrange
        var started = await StartOfflineAsync();
        var trip = started with { Detail = started.Detail with { Role = TripParticipantConstants.Participant, Trip = started.Detail.Trip with { OwnerUserId = OtherUserId } } };
        // Act
        var act = () => TripSut.UpdateAsync(trip, trip.Detail.Trip with { Title = "Changed" }, CancellationToken.None);
        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ItShouldRefuseAnEditOfAFinishedTrip()
    {
        // Arrange
        var started = await StartOfflineAsync();
        var trip = started with { Detail = started.Detail with { Trip = started.Detail.Trip with { Status = TripConstants.Completed, EndedOn = Now } } };
        // Act
        var act = () => TripSut.UpdateAsync(trip, trip.Detail.Trip with { Title = "Changed" }, CancellationToken.None);
        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ItShouldRefuseAnEditThatNamesADifferentTrip()
    {
        // Arrange
        var trip = await StartOfflineAsync();
        // Act
        var act = () => TripSut.UpdateAsync(trip, trip.Detail.Trip with { Id = Guid.NewGuid() }, CancellationToken.None);
        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ItShouldRefuseAnEditThatChangesTheOwner()
    {
        // Arrange
        var trip = await StartOfflineAsync();
        // Act
        var act = () => TripSut.UpdateAsync(trip, trip.Detail.Trip with { OwnerUserId = OtherUserId }, CancellationToken.None);
        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Theory]
    [InlineData("Unknown", false, false)]
    [InlineData(TripConstants.Active, true, false)]
    [InlineData(TripConstants.Completed, false, true)]
    public async Task ItShouldRejectAnInvalidLifecycle(string status, bool endsWhileActive, bool endsBeforeItStarted)
    {
        // Arrange
        var trip = await StartOfflineAsync();
        var edited = trip.Detail.Trip with
        {
            Status = status,
            EndedOn = endsWhileActive ? Now : endsBeforeItStarted ? trip.Detail.Trip.StartedOn.AddMinutes(-1) : null
        };
        // Act
        var act = () => TripSut.UpdateAsync(trip, edited, CancellationToken.None);
        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ItShouldRejectATripThatStartsInTheFuture()
    {
        // Arrange
        var trip = await StartOfflineAsync();
        // Act
        var act = () => TripSut.UpdateAsync(trip, trip.Detail.Trip with { StartedOn = Now.AddHours(2) }, CancellationToken.None);
        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ItShouldPersistTheTrimmedTitleAndPlaceAndQueueAMetadataUpsertAcrossARestart()
    {
        // Arrange
        var trip = await StartOfflineAsync();
        // Act
        var saved = await TripSut.UpdateAsync(trip, trip.Detail.Trip with { Title = "  Corrib weekend ", PlaceName = " Lough Mask " }, CancellationToken.None);
        // Assert
        saved.Detail.Trip.Title.Should().Be("Corrib weekend");
        saved.Detail.Trip.PlaceName.Should().Be("Lough Mask");
        saved.Revision.Should().Be(2);
        var reopened = await CreateTripStore().GetAsync(OwnerUserId, trip.Detail.Trip.Id, CancellationToken.None);
        reopened!.Detail.Trip.Title.Should().Be("Corrib weekend");
        reopened.Detail.Trip.PlaceName.Should().Be("Lough Mask");
        reopened.HasPendingMetadata.Should().BeTrue();
        (await OutboxStore.ListPendingAsync(OwnerUserId, CancellationToken.None))
            .Should().ContainSingle(operation => operation.AggregateId == trip.Detail.Trip.Id && operation.OperationType == OutboxOperationTypeEnum.UpsertTripMetadata);
    }

    [Fact]
    public async Task ItShouldFinishTheTripWithAnEndTime()
    {
        // Arrange
        var trip = await StartOfflineAsync();
        // Act
        var saved = await TripSut.UpdateAsync(trip, trip.Detail.Trip with { Status = TripConstants.Completed, EndedOn = Now }, CancellationToken.None);
        // Assert
        saved.Detail.Trip.Status.Should().Be(TripConstants.Completed);
        var reopened = await CreateTripStore().GetAsync(OwnerUserId, trip.Detail.Trip.Id, CancellationToken.None);
        reopened!.IsActive.Should().BeFalse();
        reopened.Detail.Trip.EndedOn.Should().Be(Now);
    }
}
