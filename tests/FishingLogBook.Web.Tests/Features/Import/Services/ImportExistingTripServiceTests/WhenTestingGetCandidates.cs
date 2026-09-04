using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Import.Enums;
using FishingLogBook.Web.Features.Import.Models;
using FishingLogBook.Web.Features.Import.Services;
using FishingLogBook.Web.Features.Trips.Clients;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Import.Services.ImportExistingTripServiceTests;

public class WhenTestingGetCandidates
{
    private static readonly DateTime StartedOn = new(2024, 6, 14, 9, 0, 0);

    [Fact]
    public async Task ItShouldUseAuthoritativeSummariesWithoutFetchingDetailsWhenTheProposalHasNoLocation()
    {
        // Arrange
        var trip = Summary(StartedOn.AddHours(-1), StartedOn.AddHours(2));
        var client = Substitute.For<ITripClient>();
        client.GetMyAsync(Arg.Any<CancellationToken>()).Returns([trip]);
        var proposal = Proposal();
        var sut = new ImportExistingTripService(client);

        // Act
        var candidates = await sut.GetCandidatesAsync([proposal], CancellationToken.None);

        // Assert
        candidates[proposal.Id].Should().ContainSingle().Which.Should().Be(trip);
        await client.Received(1).GetMyAsync(Arg.Any<CancellationToken>());
        await client.DidNotReceive().GetDetailAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldFetchOnlyTemporallyCompatibleDetailsAndExcludeAnIncompatibleLocation()
    {
        // Arrange
        var compatibleTime = Summary(StartedOn.AddHours(-1), StartedOn.AddHours(2));
        var incompatibleTime = Summary(StartedOn.AddDays(-2), StartedOn.AddDays(-2).AddHours(2));
        var client = Substitute.For<ITripClient>();
        client.GetMyAsync(Arg.Any<CancellationToken>()).Returns([compatibleTime, incompatibleTime]);
        client.GetDetailAsync(compatibleTime.Id, Arg.Any<CancellationToken>())
            .Returns(Detail(compatibleTime, 54d, -9d));
        var proposal = Proposal(new ImportLocationModel(53d, -9d, true).Accept());
        var sut = new ImportExistingTripService(client);

        // Act
        var candidates = await sut.GetCandidatesAsync([proposal], CancellationToken.None);

        // Assert
        candidates[proposal.Id].Should().BeEmpty();
        await client.Received(1).GetDetailAsync(compatibleTime.Id, Arg.Any<CancellationToken>());
        await client.DidNotReceive().GetDetailAsync(incompatibleTime.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotRejectACompatibleCandidateWhenTheAuthoritativeTripHasNoCoordinates()
    {
        // Arrange
        var trip = Summary(StartedOn.AddHours(-1), StartedOn.AddHours(2));
        var client = Substitute.For<ITripClient>();
        client.GetMyAsync(Arg.Any<CancellationToken>()).Returns([trip]);
        client.GetDetailAsync(trip.Id, Arg.Any<CancellationToken>()).Returns(Detail(trip));
        var proposal = Proposal(new ImportLocationModel(53d, -9d, true).Accept());
        var sut = new ImportExistingTripService(client);

        // Act
        var candidates = await sut.GetCandidatesAsync([proposal], CancellationToken.None);

        // Assert
        candidates[proposal.Id].Should().ContainSingle().Which.Should().Be(trip);
        await client.Received(1).GetDetailAsync(trip.Id, Arg.Any<CancellationToken>());
    }

    private static ImportTripProposalModel Proposal(ImportLocationModel? location = null)
    {
        return new ImportTripProposalModel(
            Guid.NewGuid(),
            [Guid.NewGuid(), Guid.NewGuid()],
            location is null ? ImportTripSuggestionConfidenceEnum.Weak : ImportTripSuggestionConfidenceEnum.Strong,
            [ImportTripSuggestionReasonEnum.ContinuousTime],
            StartedOn,
            StartedOn.AddHours(1),
            location);
    }

    private static TripSummaryDto Summary(DateTime startedOn, DateTime endedOn)
    {
        return new TripSummaryDto(
            Guid.NewGuid(),
            TripConstants.Completed,
            new DateTimeOffset(startedOn, TimeSpan.Zero),
            new DateTimeOffset(endedOn, TimeSpan.Zero))
        {
            Role = TripParticipantConstants.Owner
        };
    }

    private static TripDetailDto Detail(TripSummaryDto summary, double? latitude = null, double? longitude = null)
    {
        var location = latitude.HasValue
            ? new TripLocationDto(latitude.Value, longitude!.Value, null, summary.StartedOn, "Historical", "Private", "v1")
            : null;
        return new TripDetailDto(new TripViewDto(
            summary.Id,
            summary.OwnerUserId,
            summary.Status,
            summary.StartedOn,
            summary.EndedOn,
            location));
    }
}
