using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Trips.Services.TripServiceTests;

public sealed class WhenTestingLifecycleEdges : BaseTripServiceTest
{
    [Fact]
    public async Task ItShouldRefuseANoteForATripThatBelongsToAnotherAccount()
    {
        // Arrange
        MockConnectivity.IsOnline.Returns(false);
        var started = await TripSut.StartAsync("Corrib", null, CancellationToken.None);
        var foreign = started with { UserId = OtherUserId };
        var note = new TripNoteDto(Guid.NewGuid(), started.Detail.Trip.Id, "Fish rising", Now) { CreatedByUserId = OwnerUserId };
        // Act
        var act = () => TripSut.SaveNoteAsync(foreign, note, deleted: false, CancellationToken.None);
        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ItShouldKeepTheCaptureTimeOrderAndFallBackToTheAddedTimeForPhotographsWithoutOne()
    {
        // Arrange
        MockConnectivity.IsOnline.Returns(false);
        var trip = await TripSut.StartAsync("Corrib", null, CancellationToken.None);
        var withCapture = new TripPhotographViewDto(Guid.NewGuid(), "image/jpeg", Now, null, Now.AddHours(-3)) { ContributedByUserId = OwnerUserId };
        var withoutCapture = new TripPhotographViewDto(Guid.NewGuid(), "image/jpeg", Now.AddHours(-2), null) { ContributedByUserId = OwnerUserId };
        await TripSut.SavePhotographAsync(trip, withoutCapture, "photos/b.jpg", deleted: false, CancellationToken.None);
        await TripSut.SavePhotographAsync(trip, withCapture, "photos/a.jpg", deleted: false, CancellationToken.None);
        // Act
        var read = await TripSut.GetAsync(trip.Detail.Trip.Id, CancellationToken.None);
        // Assert
        read!.Detail.Photographs.Select(photo => photo.Id).Should().Equal(withCapture.Id, withoutCapture.Id);
    }

    [Fact]
    public async Task ItShouldNotLetASynchronisedLocalPhotographReplaceTheServerCopy()
    {
        // Arrange
        MockConnectivity.IsOnline.Returns(false);
        var trip = await TripSut.StartAsync("Corrib", null, CancellationToken.None);
        var photo = new TripPhotographViewDto(Guid.NewGuid(), "image/jpeg", Now, null) { ContributedByUserId = OwnerUserId };
        await TripSut.SavePhotographAsync(trip, photo, "photos/a.jpg", deleted: false, CancellationToken.None);
        var contribution = (await TripStore.ListContributionsAsync(OwnerUserId, trip.Detail.Trip.Id, CancellationToken.None)).Single();
        await using (var connection = await ConnectionFactory.OpenAsync(CancellationToken.None))
        await using (var transaction = (Microsoft.Data.Sqlite.SqliteTransaction)await connection.BeginTransactionAsync())
        {
            await TripStore.ConfirmContributionAsync(connection, transaction, contribution, CancellationToken.None);
            await transaction.CommitAsync();
        }

        MockConnectivity.IsOnline.Returns(true);
        var server = ServerTrip(trip.Detail.Trip.Id) with { Photographs = [photo with { Url = "https://img/server.jpg" }] };
        MockClient.GetAsync(trip.Detail.Trip.Id, Arg.Any<CancellationToken>()).Returns(ApiResultModel<TripDetailDto>.Succeeded(server));
        // Act
        var read = await TripSut.GetAsync(trip.Detail.Trip.Id, CancellationToken.None);
        // Assert
        read!.Detail.Photographs.Should().ContainSingle().Which.Url.Should().Be("https://img/server.jpg");
    }

    [Fact]
    public async Task ItShouldListWhatTheServerReturnsEvenWhenTheCachedCopyWasRevoked()
    {
        // Arrange
        var server = ServerTrip(Guid.NewGuid());
        MockClient.GetAsync(server.Trip.Id, Arg.Any<CancellationToken>()).Returns(ApiResultModel<TripDetailDto>.Succeeded(server));
        await TripSut.GetAsync(server.Trip.Id, CancellationToken.None);
        await TripStore.RevokeAsync(OwnerLease, server.Trip.Id, CancellationToken.None);
        MockClient.ListAsync(Arg.Any<CancellationToken>()).Returns(ApiResultModel<List<TripSummaryDto>>.Succeeded([Summary(server)]));
        // Act
        var history = await TripSut.LoadAsync(CancellationToken.None);
        // Assert
        history.Items.Select(trip => trip.Id).Should().Equal(server.Trip.Id);
        history.ServerUnavailable.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldRejectANoteWhoseTextIsOnlySpaces()
    {
        // Arrange
        MockConnectivity.IsOnline.Returns(false);
        var trip = await TripSut.StartAsync("Corrib", null, CancellationToken.None);
        var note = new TripNoteDto(Guid.NewGuid(), trip.Detail.Trip.Id, "   ", Now) { CreatedByUserId = OwnerUserId };
        // Act
        var act = () => TripSut.SaveNoteAsync(trip, note, deleted: false, CancellationToken.None);
        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
