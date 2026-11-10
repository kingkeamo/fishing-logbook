using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Import.Enums;
using FishingLogBook.Web.Features.Import.Models;
using FishingLogBook.Web.Features.Import.Services;
using FishingLogBook.Web.Features.Trips.Clients;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Import.Services.ImportExistingTripServiceTests;

public class WhenTestingTemporalCompatibility
{
    private static readonly DateTime ProposalStart = new(2024, 6, 14, 9, 0, 0);

    private static ImportTripProposalModel Proposal()
    {
        return new ImportTripProposalModel(
            Guid.NewGuid(),
            [Guid.NewGuid()],
            ImportTripSuggestionConfidenceEnum.Strong,
            [],
            ProposalStart,
            ProposalStart.AddHours(2));
    }

    private static TripSummaryDto Trip(
        string status = TripConstants.Completed,
        string role = TripParticipantConstants.Owner,
        DateTime? startedOn = null,
        DateTime? endedOn = null,
        bool ended = true)
    {
        var started = startedOn ?? ProposalStart.AddHours(-1);
        return new TripSummaryDto(
            Guid.NewGuid(),
            status,
            new DateTimeOffset(started, TimeSpan.Zero),
            ended ? new DateTimeOffset(endedOn ?? started.AddHours(3), TimeSpan.Zero) : null)
        {
            Role = role
        };
    }

    private static async Task<IReadOnlyList<TripSummaryDto>> CandidatesAsync(TripSummaryDto trip)
    {
        var client = Substitute.For<ITripClient>();
        client.GetMyAsync(Arg.Any<CancellationToken>()).Returns([trip]);
        var proposal = Proposal();
        var sut = new ImportExistingTripService(client);

        var candidates = await sut.GetCandidatesAsync([proposal], CancellationToken.None);

        await client.Received(1).GetMyAsync(Arg.Any<CancellationToken>());
        return candidates[proposal.Id];
    }

    [Theory]
    [InlineData(TripConstants.Active)]
    [InlineData(TripConstants.Planned)]
    [InlineData(TripConstants.Cancelled)]
    public async Task ItShouldIgnoreATripThatIsNotCompleted(string status)
    {
        // Arrange
        var trip = Trip(status);

        // Act
        var candidates = await CandidatesAsync(trip);

        // Assert
        candidates.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldIgnoreATripTheAnglerOnlyObserves()
    {
        // Arrange
        var trip = Trip(role: "Observer");

        // Act
        var candidates = await CandidatesAsync(trip);

        // Assert
        candidates.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldOfferATripTheAnglerParticipatedIn()
    {
        // Arrange
        var trip = Trip(role: TripParticipantConstants.Participant);

        // Act
        var candidates = await CandidatesAsync(trip);

        // Assert
        candidates.Should().ContainSingle().Which.Should().Be(trip);
    }

    [Fact]
    public async Task ItShouldIgnoreACompletedTripWithoutAnEndTime()
    {
        // Arrange
        var trip = Trip(ended: false);

        // Act
        var candidates = await CandidatesAsync(trip);

        // Assert
        candidates.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldIgnoreATripThatStartsTheDayAfterTheProposalEnds()
    {
        // Arrange
        var trip = Trip(startedOn: ProposalStart.Date.AddDays(1), endedOn: ProposalStart.Date.AddDays(1).AddHours(4));

        // Act
        var candidates = await CandidatesAsync(trip);

        // Assert
        candidates.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldIgnoreATripThatEndedTheDayBeforeTheProposalStarts()
    {
        // Arrange
        var trip = Trip(startedOn: ProposalStart.Date.AddDays(-1), endedOn: ProposalStart.Date.AddSeconds(-1));

        // Act
        var candidates = await CandidatesAsync(trip);

        // Assert
        candidates.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldOfferATripThatStartsOnTheLastDayOfTheProposalAndATripThatEndsOnItsFirstDay()
    {
        // Arrange
        var startsOnLastDay = Trip(
            startedOn: ProposalStart.Date.AddHours(23),
            endedOn: ProposalStart.Date.AddDays(1).AddHours(2));
        var endsOnFirstDay = Trip(
            startedOn: ProposalStart.Date.AddDays(-1).AddHours(20),
            endedOn: ProposalStart.Date.AddHours(1));

        // Act
        var first = await CandidatesAsync(startsOnLastDay);
        var second = await CandidatesAsync(endsOnFirstDay);

        // Assert
        first.Should().ContainSingle().Which.Should().Be(startsOnLastDay);
        second.Should().ContainSingle().Which.Should().Be(endsOnFirstDay);
    }
}
