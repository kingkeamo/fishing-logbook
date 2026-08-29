using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Tests.Common;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Trips.Services.TripDetailServiceTests;

public class WhenTestingSharedTripDetail : BaseTripDetailServiceTest
{
    [Fact]
    public async Task ItShouldTellTheOwnerTheyOwnTheTrip()
    {
        // Act
        var result = await Sut.GetAsync(new GetTripArgs { TripId = TripId }, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Role.Should().Be(TripParticipantConstants.Owner);
    }

    [Fact]
    public async Task ItShouldGiveAParticipantTheSameTripIdAndTheParticipantRole()
    {
        // Arrange
        MockTripAccessService.GivenParticipant(StoredTrip(ownerUserId: OtherUserId), CurrentUserId);

        // Act
        var result = await Sut.GetAsync(new GetTripArgs { TripId = TripId }, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Role.Should().Be(TripParticipantConstants.Participant);
        result.Value.Trip.Id.Should().Be(TripId);
        result.Value.Trip.OwnerUserId.Should().Be(OtherUserId);
    }

    [Fact]
    public async Task ItShouldShowEveryContributorsEntriesInOneTimeline()
    {
        // Arrange
        MockTripAccessService.GivenParticipant(StoredTrip(ownerUserId: OtherUserId), CurrentUserId);
        MockTripNoteRepository.GetByTripIdAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<TripNote>>(
            [
                new TripNote
                {
                    Id = Guid.NewGuid(),
                    TripId = TripId,
                    CreatedByUserId = OtherUserId,
                    Text = "owner note",
                    RecordedOn = StartedOn.AddMinutes(5)
                },
                new TripNote
                {
                    Id = Guid.NewGuid(),
                    TripId = TripId,
                    CreatedByUserId = CurrentUserId,
                    Text = "participant note",
                    RecordedOn = StartedOn.AddMinutes(20)
                }
            ]));
        MockTripPhotographRepository.GetByTripIdAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<TripPhotograph>>(
                [Photograph("trips/photo.jpg", StartedOn.AddMinutes(10), CurrentUserId)]));

        // Act
        var result = await Sut.GetAsync(new GetTripArgs { TripId = TripId }, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Notes.Select(note => note.CreatedByUserId)
            .Should().Equal([OtherUserId, CurrentUserId]);
        result.Value.Photographs[0].ContributedByUserId.Should().Be(CurrentUserId);
        await MockTripNoteRepository.Received(1).GetByTripIdAsync(TripId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAttributeCatchesToTheAnglerWhoCaughtThem()
    {
        // Arrange
        MockTripAccessService.GivenParticipant(StoredTrip(ownerUserId: OtherUserId), CurrentUserId);
        MockTripRepository.GetCatchSummariesByTripIdAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<TripCatchSummary>>(
            [
                new TripCatchSummary
                {
                    Id = Guid.NewGuid(),
                    UserId = CurrentUserId,
                    AnglerUserId = CurrentUserId,
                    CaughtOn = StartedOn.AddMinutes(30),
                    SpeciesName = "Pike"
                }
            ]));

        // Act
        var result = await Sut.GetAsync(new GetTripArgs { TripId = TripId }, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Catches[0].AnglerUserId.Should().Be(CurrentUserId);
    }

    [Fact]
    public async Task ItShouldDescribeEveryContributorWithTheirPrivacyFilteredName()
    {
        // Arrange
        MockTripAccessService.GivenParticipant(StoredTrip(ownerUserId: OtherUserId), CurrentUserId);
        MockTripNoteRepository.GetByTripIdAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<TripNote>>(
            [
                new TripNote
                {
                    Id = Guid.NewGuid(),
                    TripId = TripId,
                    CreatedByUserId = CurrentUserId,
                    Text = "participant note",
                    RecordedOn = StartedOn.AddMinutes(20)
                }
            ]));
        MockAnglerLookupService.DescribeAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyDictionary<Guid, AnglerSummaryDto>>(
                new Dictionary<Guid, AnglerSummaryDto>
                {
                    [OtherUserId] = new(OtherUserId, "Mark", null, null)
                }));

        // Act
        var result = await Sut.GetAsync(new GetTripArgs { TripId = TripId }, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Contributors.Single(contributor => contributor.UserId == OtherUserId)
            .IsOwner.Should().BeTrue();
        result.Value.Contributors.Single(contributor => contributor.UserId == OtherUserId)
            .DisplayName.Should().Be("Mark");
        result.Value.Contributors.Single(contributor => contributor.UserId == CurrentUserId)
            .DisplayName.Should().BeNull();
        await MockAnglerLookupService.Received(1).DescribeAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(userIds =>
                userIds.Contains(CurrentUserId) && userIds.Contains(OtherUserId)),
            Arg.Any<CancellationToken>());
    }
}
