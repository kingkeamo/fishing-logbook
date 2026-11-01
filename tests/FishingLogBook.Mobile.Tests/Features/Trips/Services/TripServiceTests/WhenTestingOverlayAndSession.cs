using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.Services.TripServiceTests;

public sealed class WhenTestingOverlayAndSession : BaseTripServiceTest
{
    [Fact]
    public async Task ItShouldStopWhenTheSessionEndsWhileReadingATrip()
    {
        // Arrange
        MockConnectivity.IsOnline.Returns(false);
        MockSessionCoordinator.IsCurrent(Arg.Any<AccountSessionLeaseModel>()).Returns(false);
        // Act
        var act = () => TripSut.GetAsync(Guid.NewGuid(), CancellationToken.None);
        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ItShouldStopWhenTheSessionEndsWhileLoadingTheHistory()
    {
        // Arrange
        MockConnectivity.IsOnline.Returns(false);
        MockSessionCoordinator.IsCurrent(Arg.Any<AccountSessionLeaseModel>()).Returns(false);
        // Act
        var act = () => TripSut.LoadAsync(CancellationToken.None);
        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ItShouldNotCacheAFinishedServerTripThatWasNeverOnThisDevice()
    {
        // Arrange
        var server = ServerTrip(Guid.NewGuid(), TripConstants.Completed);
        MockClient.GetAsync(server.Trip.Id, Arg.Any<CancellationToken>()).Returns(ApiResultModel<TripDetailDto>.Succeeded(server));
        // Act
        var trip = await TripSut.GetAsync(server.Trip.Id, CancellationToken.None);
        // Assert
        trip.Should().NotBeNull();
        (await TripStore.ListAsync(OwnerUserId, CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldKeepALocalUnsyncedEditOverTheServerTitleButShowTheServerContributions()
    {
        // Arrange
        MockConnectivity.IsOnline.Returns(false);
        var local = await TripSut.StartAsync("Local title", null, CancellationToken.None);
        MockConnectivity.IsOnline.Returns(true);
        var server = ServerTrip(local.Detail.Trip.Id) with { Notes = [new TripNoteDto(Guid.NewGuid(), local.Detail.Trip.Id, "From server", Now.AddHours(-1)) { CreatedByUserId = OwnerUserId }] };
        MockClient.GetAsync(local.Detail.Trip.Id, Arg.Any<CancellationToken>()).Returns(ApiResultModel<TripDetailDto>.Succeeded(server));
        // Act
        var trip = await TripSut.GetAsync(local.Detail.Trip.Id, CancellationToken.None);
        // Assert
        trip!.Detail.Trip.Title.Should().Be("Local title");
        trip.Detail.Notes.Should().ContainSingle(note => note.Text == "From server");
    }

    [Fact]
    public async Task ItShouldNotLetASynchronisedLocalContributionReplaceWhatTheServerAlreadyHas()
    {
        // Arrange
        MockConnectivity.IsOnline.Returns(false);
        var trip = await TripSut.StartAsync("Corrib", null, CancellationToken.None);
        var noteId = Guid.NewGuid();
        var note = new TripNoteDto(noteId, trip.Detail.Trip.Id, "Local text", trip.Detail.Trip.StartedOn) { CreatedByUserId = OwnerUserId };
        await TripSut.SaveNoteAsync(trip, note, deleted: false, CancellationToken.None);
        var contribution = (await TripStore.ListContributionsAsync(OwnerUserId, trip.Detail.Trip.Id, CancellationToken.None)).Single();
        await using (var connection = await ConnectionFactory.OpenAsync(CancellationToken.None))
        await using (var transaction = (Microsoft.Data.Sqlite.SqliteTransaction)await connection.BeginTransactionAsync())
        {
            await TripStore.ConfirmContributionAsync(connection, transaction, contribution, CancellationToken.None);
            await transaction.CommitAsync();
        }
        MockConnectivity.IsOnline.Returns(true);
        var server = ServerTrip(trip.Detail.Trip.Id) with { Notes = [note with { Text = "Server text" }] };
        MockClient.GetAsync(trip.Detail.Trip.Id, Arg.Any<CancellationToken>()).Returns(ApiResultModel<TripDetailDto>.Succeeded(server));
        // Act
        var read = await TripSut.GetAsync(trip.Detail.Trip.Id, CancellationToken.None);
        // Assert
        read!.Detail.Notes.Should().ContainSingle().Which.Text.Should().Be("Server text");
    }

    [Fact]
    public async Task ItShouldOrderPhotographsByCaptureTime()
    {
        // Arrange
        MockConnectivity.IsOnline.Returns(false);
        var trip = await TripSut.StartAsync("Corrib", null, CancellationToken.None);
        var later = new TripPhotographViewDto(Guid.NewGuid(), "image/jpeg", Now, null, Now.AddHours(-1)) { ContributedByUserId = OwnerUserId };
        var earlier = new TripPhotographViewDto(Guid.NewGuid(), "image/jpeg", Now, null, Now.AddHours(-2)) { ContributedByUserId = OwnerUserId };
        await TripSut.SavePhotographAsync(trip, later, "photos/later.jpg", deleted: false, CancellationToken.None);
        await TripSut.SavePhotographAsync(trip, earlier, "photos/earlier.jpg", deleted: false, CancellationToken.None);
        // Act
        var read = await TripSut.GetAsync(trip.Detail.Trip.Id, CancellationToken.None);
        // Assert
        read!.Detail.Photographs.Select(photo => photo.Id).Should().Equal(earlier.Id, later.Id);
    }
}
