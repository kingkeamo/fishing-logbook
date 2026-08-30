using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Capabilities.Errors;
using FishingLogBook.Application.Tests.Common;
using FishingLogBook.Application.Trips.Errors;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Shared.Constants;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Services.TripDetailServiceTests;

public class WhenTestingGet : BaseTripDetailServiceTest
{
    [Fact]
    public async Task ItShouldRejectAnUnresolvedCaller()
    {
        // Arrange
        MockCurrentUser.IsResolved.Returns(false);
        MockTripAccessService.RequireContributorAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<TripAccess>(new CurrentUserUnresolvedError()));

        // Act
        var result = await Sut.GetAsync(new GetTripArgs { TripId = TripId }, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<CurrentUserUnresolvedError>();
        await MockTripRepository.DidNotReceive().GetCatchSummariesByTripIdAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
        await MockTripNoteRepository.DidNotReceive().GetByTripIdAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportNotFoundWhenTheTripDoesNotExist()
    {
        // Arrange
        MockTripAccessService.GivenNoAccess(TripId);

        // Act
        var result = await Sut.GetAsync(new GetTripArgs { TripId = TripId }, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripNotFoundError>();
        await MockTripPhotographRepository.DidNotReceive().GetByTripIdAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportNotFoundForAnotherAnglersTrip()
    {
        // Arrange
        MockTripAccessService.GivenOwner(StoredTrip(ownerUserId: OtherUserId), CurrentUserId);

        // Act
        var result = await Sut.GetAsync(new GetTripArgs { TripId = TripId }, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<TripNotFoundError>();
        await MockTripRepository.DidNotReceive().GetCatchSummariesByTripIdAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
        await MockTripNoteRepository.DidNotReceive().GetByTripIdAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheFailureWhenTheNotesCannotBeRead()
    {
        // Arrange
        MockTripNoteRepository.GetByTripIdAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<IReadOnlyList<TripNote>>("Failed to load the trip notes."));

        // Act
        var result = await Sut.GetAsync(new GetTripArgs { TripId = TripId }, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("Failed to load the trip notes.");
    }

    [Fact]
    public async Task ItShouldReturnTheFailureWhenThePhotographsCannotBeRead()
    {
        // Arrange
        MockTripPhotographRepository.GetByTripIdAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<IReadOnlyList<TripPhotograph>>("Failed to load the trip photographs."));

        // Act
        var result = await Sut.GetAsync(new GetTripArgs { TripId = TripId }, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("Failed to load the trip photographs.");
    }

    [Fact]
    public async Task ItShouldReturnTheFailureWhenTheCatchSummariesCannotBeRead()
    {
        // Arrange
        MockTripRepository.GetCatchSummariesByTripIdAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<IReadOnlyList<TripCatchSummary>>("Failed to save the trip."));

        // Act
        var result = await Sut.GetAsync(new GetTripArgs { TripId = TripId }, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldReturnAnEmptyTripWithNoNotesPhotographsOrCatches()
    {
        // Arrange
        // Act
        var result = await Sut.GetAsync(new GetTripArgs { TripId = TripId }, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Trip.Id.Should().Be(TripId);
        result.Value.Trip.Status.Should().Be(TripConstants.Active);
        result.Value.Notes.Should().BeEmpty();
        result.Value.Photographs.Should().BeEmpty();
        result.Value.Catches.Should().BeEmpty();
        await MockObjectStorage.DidNotReceive().CreateDownloadUrlAsync(
            Arg.Any<string>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldLeaveThePhotographUrlEmptyWhenStorageIsNotConfigured()
    {
        // Arrange
        MockObjectStorage.IsConfigured.Returns(false);
        MockTripPhotographRepository.GetByTripIdAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<TripPhotograph>>(
                [Photograph("trips/one.jpg", StartedOn.AddMinutes(10))]));

        // Act
        var result = await Sut.GetAsync(new GetTripArgs { TripId = TripId }, CancellationToken.None);

        // Assert
        result.Value.Photographs.Single().Url.Should().BeNull();
        await MockObjectStorage.DidNotReceive().CreateDownloadUrlAsync(
            Arg.Any<string>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheCompletedTripWithItsTimelineContent()
    {
        // Arrange
        MockTripAccessService.GivenOwner(
            StoredTrip(
                status: TripStatusEnum.Completed,
                endedOn: StartedOn.AddHours(6),
                title: "Day with Dad",
                placeName: "Lough Corrib"),
            CurrentUserId);
        MockTripNoteRepository.GetByTripIdAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<TripNote>>(
            [
                Note("Second", StartedOn.AddHours(2)),
                Note("First", StartedOn.AddMinutes(5))
            ]));
        MockTripPhotographRepository.GetByTripIdAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<TripPhotograph>>(
            [
                Photograph("trips/late.jpg", StartedOn.AddHours(3)),
                Photograph("trips/early.jpg", StartedOn.AddMinutes(20))
            ]));
        MockTripRepository.GetCatchSummariesByTripIdAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<TripCatchSummary>>(
            [
                CatchSummary("Pike", StartedOn.AddHours(1)),
                CatchSummary(null, StartedOn.AddHours(4))
            ]));

        // Act
        var result = await Sut.GetAsync(new GetTripArgs { TripId = TripId }, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Trip.PlaceName.Should().Be("Lough Corrib");
        result.Value.Trip.Status.Should().Be(TripConstants.Completed);
        result.Value.Notes.Select(note => note.Text).Should().Equal("First", "Second");
        result.Value.Notes.Should().OnlyContain(note => note.CreatedByUserId == CurrentUserId);
        result.Value.Photographs.Select(photograph => photograph.Url)
            .Should().Equal(
                "https://storage.test/trips/early.jpg?signed=1",
                "https://storage.test/trips/late.jpg?signed=1");
        result.Value.Catches.Select(summary => summary.SpeciesName).Should().Equal("Pike", null);
        await MockTripRepository.Received(1).GetCatchSummariesByTripIdAsync(
            TripId,
            Arg.Any<CancellationToken>());
        await MockObjectStorage.Received(2).CreateDownloadUrlAsync(
            Arg.Any<string>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }
}
