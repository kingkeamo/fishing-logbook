using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Shared.Constants;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Services.TripServiceTests;

public class WhenTestingGetSummaries : BaseTripServiceTest
{
    [Fact]
    public async Task ItShouldReturnTheRepositoryFailure()
    {
        // Arrange
        MockTripRepository.GetSummariesForUserAsync(
                Arg.Is<GetMyTripsArgs>(args => args.UserId == CurrentUserId),
                Arg.Any<CancellationToken>())
            .Returns(Result.Fail<IReadOnlyList<TripSummary>>("Failed to save the trip."));

        // Act
        var result = await Sut.GetSummariesAsync(
            new GetMyTripsArgs { UserId = CurrentUserId },
            CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        await MockTripRepository.Received(1).GetSummariesForUserAsync(
            Arg.Is<GetMyTripsArgs>(args => args.UserId == CurrentUserId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnAnEmptyListWhenTheAnglerHasNoTrips()
    {
        // Arrange
        MockTripRepository.GetSummariesForUserAsync(
                Arg.Is<GetMyTripsArgs>(args => args.UserId == CurrentUserId),
                Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<TripSummary>>([]));

        // Act
        var result = await Sut.GetSummariesAsync(
            new GetMyTripsArgs { UserId = CurrentUserId },
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldRequestOnlyTheGivenOwnersTrips()
    {
        // Arrange
        MockTripRepository.GetSummariesForUserAsync(
                Arg.Is<GetMyTripsArgs>(args => args.UserId == CurrentUserId),
                Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<TripSummary>>([Summary()]));

        // Act
        var result = await Sut.GetSummariesAsync(
            new GetMyTripsArgs { UserId = CurrentUserId },
            CancellationToken.None);

        // Assert
        result.Value.Should().ContainSingle();
        await MockTripRepository.Received(1).GetSummariesForUserAsync(
            Arg.Is<GetMyTripsArgs>(args => args.UserId == CurrentUserId),
            Arg.Any<CancellationToken>());
        await MockTripRepository.DidNotReceive().GetSummariesForUserAsync(
            Arg.Is<GetMyTripsArgs>(args => args.UserId == OtherUserId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnBothActiveAndCompletedTripsWithTheirCounts()
    {
        // Arrange
        var completedId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        MockTripRepository.GetSummariesForUserAsync(
                Arg.Is<GetMyTripsArgs>(args => args.UserId == CurrentUserId),
                Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<TripSummary>>(
            [
                Summary(),
                Summary(
                    tripId: completedId,
                    status: TripStatusEnum.Completed,
                    endedOn: StartedOn.AddHours(4),
                    catchCount: 3,
                    photographCount: 2,
                    noteCount: 1)
            ]));

        // Act
        var result = await Sut.GetSummariesAsync(
            new GetMyTripsArgs { UserId = CurrentUserId },
            CancellationToken.None);

        // Assert
        result.Value.Should().HaveCount(2);
        var active = result.Value.Single(summary => summary.Id == TripId);
        active.Status.Should().Be(TripConstants.Active);
        active.EndedOn.Should().BeNull();
        active.CatchCount.Should().Be(0);
        var completed = result.Value.Single(summary => summary.Id == completedId);
        completed.Status.Should().Be(TripConstants.Completed);
        completed.EndedOn.Should().Be(StartedOn.AddHours(4));
        completed.CatchCount.Should().Be(3);
        completed.PhotographCount.Should().Be(2);
        completed.NoteCount.Should().Be(1);
    }

    [Fact]
    public async Task ItShouldReturnTheTitleAndPlaceSnapshot()
    {
        // Arrange
        MockTripRepository.GetSummariesForUserAsync(
                Arg.Is<GetMyTripsArgs>(args => args.UserId == CurrentUserId),
                Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<TripSummary>>(
                [Summary(title: "Morning session", placeName: "Lough Corrib")]));

        // Act
        var result = await Sut.GetSummariesAsync(
            new GetMyTripsArgs { UserId = CurrentUserId },
            CancellationToken.None);

        // Assert
        result.Value[0].Title.Should().Be("Morning session");
        result.Value[0].PlaceName.Should().Be("Lough Corrib");
        result.Value[0].StartedOn.Should().Be(StartedOn);
    }

    private static TripSummary Summary(
        Guid? tripId = null,
        TripStatusEnum status = TripStatusEnum.Active,
        DateTimeOffset? endedOn = null,
        string? title = null,
        string? placeName = null,
        int catchCount = 0,
        int photographCount = 0,
        int noteCount = 0,
        Guid? ownerUserId = null,
        int participantCount = 0)
    {
        return new TripSummary
        {
            Id = tripId ?? TripId,
            OwnerUserId = ownerUserId ?? CurrentUserId,
            Status = status,
            StartedOn = StartedOn,
            EndedOn = endedOn,
            Title = title,
            PlaceName = placeName,
            CatchCount = catchCount,
            PhotographCount = photographCount,
            NoteCount = noteCount,
            ParticipantCount = participantCount
        };
    }
}
