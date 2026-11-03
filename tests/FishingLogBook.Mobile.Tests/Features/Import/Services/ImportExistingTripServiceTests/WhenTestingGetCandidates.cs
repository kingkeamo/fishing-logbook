using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Import.Enums;
using FishingLogBook.Mobile.Features.Import.Models;
using FishingLogBook.Mobile.Features.Import.Services;
using FishingLogBook.Mobile.Features.Trips.Clients;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Import.Services.ImportExistingTripServiceTests;

public class WhenTestingGetCandidates
{
    private static readonly DateTime StartedOn = new(2024, 6, 14, 9, 0, 0);

    [Fact]
    public async Task ItShouldOfferAnActiveTripOnTheSameDate()
    {
        // Arrange
        var trip = Summary(TripConstants.Active, StartedOn);
        var client = Substitute.For<ITripClient>();
        client.ListAsync(Arg.Any<CancellationToken>()).Returns(ApiResultModel<List<TripSummaryDto>>.Succeeded([trip]));
        var proposal = Proposal();
        var sut = new ImportExistingTripService(client);

        // Act
        var candidates = await sut.GetCandidatesAsync([proposal], CancellationToken.None);

        // Assert
        candidates[proposal.Id].Should().ContainSingle().Which.Should().Be(trip);
        await client.Received(1).ListAsync(Arg.Any<CancellationToken>());
        await client.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldOfferACompletedTripOnTheSameDateWhenItsTimesDoNotOverlap()
    {
        // Arrange
        var trip = Summary(TripConstants.Completed, StartedOn.AddHours(-6), StartedOn.AddHours(-4));
        var client = Substitute.For<ITripClient>();
        client.ListAsync(Arg.Any<CancellationToken>()).Returns(ApiResultModel<List<TripSummaryDto>>.Succeeded([trip]));
        var proposal = Proposal();
        var sut = new ImportExistingTripService(client);

        // Act
        var candidates = await sut.GetCandidatesAsync([proposal], CancellationToken.None);

        // Assert
        candidates[proposal.Id].Should().ContainSingle().Which.Should().Be(trip);
        await client.Received(1).ListAsync(Arg.Any<CancellationToken>());
        await client.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldOfferTheActiveTripBeforeACompletedTripOnTheSameDate()
    {
        // Arrange
        var completed = Summary(TripConstants.Completed, StartedOn.AddHours(-2), StartedOn.AddHours(2));
        var active = Summary(TripConstants.Active, StartedOn.AddHours(-1));
        var client = Substitute.For<ITripClient>();
        client.ListAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<List<TripSummaryDto>>.Succeeded([completed, active]));
        var proposal = Proposal();
        var sut = new ImportExistingTripService(client);

        // Act
        var candidates = await sut.GetCandidatesAsync([proposal], CancellationToken.None);

        // Assert
        candidates[proposal.Id].Should().Equal(active, completed);
        await client.Received(1).ListAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldExcludeAnActiveTripThatStartsAfterTheProposedCatches()
    {
        // Arrange
        var trip = Summary(TripConstants.Active, StartedOn.AddDays(1));
        var client = Substitute.For<ITripClient>();
        client.ListAsync(Arg.Any<CancellationToken>()).Returns(ApiResultModel<List<TripSummaryDto>>.Succeeded([trip]));
        var proposal = Proposal();
        var sut = new ImportExistingTripService(client);

        // Act
        var candidates = await sut.GetCandidatesAsync([proposal], CancellationToken.None);

        // Assert
        candidates[proposal.Id].Should().BeEmpty();
        await client.Received(1).ListAsync(Arg.Any<CancellationToken>());
        await client.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldExcludePlannedAndCancelledTrips()
    {
        // Arrange
        var planned = Summary(TripConstants.Planned, StartedOn);
        var cancelled = Summary(TripConstants.Cancelled, StartedOn, StartedOn.AddHours(2));
        var client = Substitute.For<ITripClient>();
        client.ListAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<List<TripSummaryDto>>.Succeeded([planned, cancelled]));
        var proposal = Proposal();
        var sut = new ImportExistingTripService(client);

        // Act
        var candidates = await sut.GetCandidatesAsync([proposal], CancellationToken.None);

        // Assert
        candidates[proposal.Id].Should().BeEmpty();
        await client.Received(1).ListAsync(Arg.Any<CancellationToken>());
        await client.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldFetchOnlyTemporallyCompatibleDetailsAndExcludeAnIncompatibleLocation()
    {
        // Arrange
        var compatibleTime = Summary(TripConstants.Completed, StartedOn.AddHours(-1), StartedOn.AddHours(2));
        var incompatibleTime = Summary(
            TripConstants.Completed,
            StartedOn.AddDays(-2),
            StartedOn.AddDays(-2).AddHours(2));
        var client = Substitute.For<ITripClient>();
        client.ListAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<List<TripSummaryDto>>.Succeeded([compatibleTime, incompatibleTime]));
        client.GetAsync(compatibleTime.Id, Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<TripDetailDto>.Succeeded(Detail(compatibleTime, 54d, -9d)));
        var proposal = Proposal(new ImportLocationModel(53d, -9d, true).Accept());
        var sut = new ImportExistingTripService(client);

        // Act
        var candidates = await sut.GetCandidatesAsync([proposal], CancellationToken.None);

        // Assert
        candidates[proposal.Id].Should().BeEmpty();
        await client.Received(1).GetAsync(compatibleTime.Id, Arg.Any<CancellationToken>());
        await client.DidNotReceive().GetAsync(incompatibleTime.Id, Arg.Any<CancellationToken>());
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

    private static TripSummaryDto Summary(string status, DateTime startedOn, DateTime? endedOn = null)
    {
        return new TripSummaryDto(
            Guid.NewGuid(),
            status,
            new DateTimeOffset(startedOn, TimeSpan.Zero),
            endedOn is { } ended ? new DateTimeOffset(ended, TimeSpan.Zero) : null)
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
