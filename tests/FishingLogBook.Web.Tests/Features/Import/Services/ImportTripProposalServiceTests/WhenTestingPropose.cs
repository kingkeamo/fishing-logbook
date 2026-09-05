using AwesomeAssertions;
using FishingLogBook.Web.Features.Import.Enums;
using FishingLogBook.Web.Features.Import.Models;

namespace FishingLogBook.Web.Tests.Features.Import.Services.ImportTripProposalServiceTests;

public class WhenTestingPropose : BaseImportTripProposalServiceTest
{
    private const double FiveKilometresLongitude = 0.0449660181862269d;

    [Fact]
    public void ItShouldNotSuggestASingleton()
    {
        // Arrange
        var batch = Batch(new CatchSpec(TimeSpan.Zero));

        // Act
        var proposals = Sut.Propose(batch);

        // Assert
        proposals.Should().BeEmpty();
    }

    [Fact]
    public void ItShouldExcludeUnreviewedAndMissingTimestamps()
    {
        // Arrange
        var batch = Batch(
            new CatchSpec(TimeSpan.Zero, Reviewed: false),
            new CatchSpec(TimeSpan.FromHours(1), Reviewed: false, Timestamp: ImportTimestampModel.Missing()));

        // Act
        var proposals = Sut.Propose(batch);

        // Assert
        proposals.Should().BeEmpty();
    }

    [Fact]
    public void ItShouldJoinLocationsExactlyFiveKilometresApart()
    {
        // Arrange
        var batch = Batch(
            new CatchSpec(TimeSpan.Zero, 0d, 0d),
            new CatchSpec(TimeSpan.FromHours(1), 0d, FiveKilometresLongitude));

        // Act
        var proposals = Sut.Propose(batch);

        // Assert
        proposals.Should().ContainSingle().Which.Confidence.Should().Be(ImportTripSuggestionConfidenceEnum.Strong);
    }

    [Theory]
    [InlineData(5.01d)]
    [InlineData(25.01d)]
    public void ItShouldNotJoinLocationsBeyondTheNearbyBoundary(double kilometres)
    {
        // Arrange
        var longitude = FiveKilometresLongitude * kilometres / 5d;
        var batch = Batch(
            new CatchSpec(TimeSpan.Zero, 0d, 0d),
            new CatchSpec(TimeSpan.FromHours(1), 0d, longitude));

        // Act
        var proposals = Sut.Propose(batch);

        // Assert
        proposals.Should().BeEmpty();
    }

    [Fact]
    public void ItShouldCreateAWeakSuggestionWhenGpsIsMissing()
    {
        // Arrange
        var batch = Batch(
            new CatchSpec(TimeSpan.Zero, 53d, -9d),
            new CatchSpec(TimeSpan.FromHours(1)));

        // Act
        var proposal = Sut.Propose(batch).Single();

        // Assert
        proposal.Confidence.Should().Be(ImportTripSuggestionConfidenceEnum.Weak);
        proposal.Reasons.Should().Contain(ImportTripSuggestionReasonEnum.MissingGps);
        proposal.RepresentativeLocation.Should().BeNull();
    }

    [Theory]
    [InlineData(4d, 1)]
    [InlineData(4.01d, 0)]
    public void ItShouldApplyTheAdjacentGapBoundary(double hours, int expected)
    {
        // Arrange
        var batch = Batch(
            new CatchSpec(TimeSpan.Zero),
            new CatchSpec(TimeSpan.FromHours(hours)));

        // Act
        var proposals = Sut.Propose(batch);

        // Assert
        proposals.Should().HaveCount(expected);
    }

    [Fact]
    public void ItShouldAllowExactlyEighteenHoursButSplitBeyondIt()
    {
        // Arrange
        double[] boundaryHours = [-9d, -5d, -1d, 3d, 7d, 9d];
        var atBoundary = boundaryHours.Select(hours => new CatchSpec(TimeSpan.FromHours(hours))).ToArray();
        double[] beyondHours = [-9d, -5.3d, -1.6d, 2.1d, 5.8d, 9.5d];
        var beyond = beyondHours.Select(hours => new CatchSpec(TimeSpan.FromHours(hours))).ToArray();

        // Act
        var boundaryProposals = Sut.Propose(Batch(atBoundary));
        var beyondProposals = Sut.Propose(Batch(beyond));

        // Assert
        boundaryProposals.Should().ContainSingle().Which.CatchProposalIds.Should().HaveCount(6);
        beyondProposals.Should().ContainSingle().Which.CatchProposalIds.Should().HaveCount(5);
    }

    [Fact]
    public void ItShouldSplitDifferentLocalCalendarDates()
    {
        // Arrange
        var batch = Batch(
            new CatchSpec(TimeSpan.FromHours(14.5)),
            new CatchSpec(TimeSpan.FromHours(15.5)));

        // Act
        var proposals = Sut.Propose(batch);

        // Assert
        proposals.Should().BeEmpty();
    }

    [Fact]
    public void ItShouldBeDeterministicAndIgnoreMethodChanges()
    {
        // Arrange
        var batch = Batch(
            new CatchSpec(TimeSpan.FromHours(2)),
            new CatchSpec(TimeSpan.Zero),
            new CatchSpec(TimeSpan.FromHours(1)));
        batch.SetCatchMethod(
            batch.CatchProposals[1].Id,
            new ImportCatalogueSelectionModel(Guid.NewGuid(), "Bait", "Bait"));
        batch.MarkCatchReviewed(batch.CatchProposals[1].Id);

        // Act
        var first = Sut.Propose(batch).Single();
        var second = Sut.Propose(batch).Single();

        // Assert
        first.Id.Should().Be(second.Id);
        first.CatchProposalIds.Should().Equal(second.CatchProposalIds);
        first.CatchProposalIds.Should().Equal(batch.CatchProposals.OrderBy(proposal => proposal.CaughtOn.Instant).Select(proposal => proposal.Id));
    }

    [Fact]
    public void ItShouldRegenerateWithoutStaleMembershipAfterACatchChanges()
    {
        // Arrange
        var batch = Batch(new CatchSpec(TimeSpan.Zero), new CatchSpec(TimeSpan.FromHours(1)));
        batch.ReplaceTripProposals(Sut.Propose(batch));
        batch.TripProposals.Should().ContainSingle();

        // Act
        batch.SetCatchCaughtOn(batch.CatchProposals[1].Id, ImportTimestampModel.Missing());
        batch.ReplaceTripProposals(Sut.Propose(batch));

        // Assert
        batch.TripProposals.Should().BeEmpty();
    }

    [Fact]
    public void ItShouldIgnorePlaceLabelsWhenGrouping()
    {
        // Arrange
        var firstLabel = new ImportLocationLookupResultModel("Galway, Ireland", "Galway", null, "Ireland");
        var secondLabel = new ImportLocationLookupResultModel("Different label", "Spiddal", null, "Ireland");
        var batch = Batch(
            new CatchSpec(TimeSpan.Zero, 53d, -9d, LookupResult: firstLabel),
            new CatchSpec(TimeSpan.FromHours(1), 53d, -9d, LookupResult: secondLabel));

        // Act
        var proposals = Sut.Propose(batch);

        // Assert
        proposals.Should().ContainSingle();
    }

    [Fact]
    public void ItShouldIgnoreRemovedCatches()
    {
        // Arrange
        var batch = Batch(
            new CatchSpec(TimeSpan.Zero),
            new CatchSpec(TimeSpan.FromHours(1)),
            new CatchSpec(TimeSpan.FromHours(2)));
        batch.RemoveCatchProposal(batch.CatchProposals[1].Id);

        // Act
        var proposal = Sut.Propose(batch).Single();

        // Assert
        proposal.CatchProposalIds.Should().Equal(batch.CatchProposals[0].Id, batch.CatchProposals[2].Id);
    }

    [Fact]
    public void ItShouldUseAConfirmedOffsetlessWallClockWithoutInventingAnOffset()
    {
        // Arrange
        var firstLocal = new DateTime(2024, 6, 14, 9, 0, 0);
        var secondLocal = new DateTime(2024, 6, 14, 10, 0, 0);
        var first = ImportTimestampModel.FromLocalWallClock(firstLocal, ImportTimestampSourceEnum.ExifOriginal)
            .ConfirmLocalWallClock(firstLocal, TimeSpan.FromHours(1));
        var second = ImportTimestampModel.FromLocalWallClock(secondLocal, ImportTimestampSourceEnum.ExifOriginal)
            .ConfirmLocalWallClock(secondLocal, TimeSpan.FromHours(1));
        var batch = Batch(
            new CatchSpec(TimeSpan.Zero, Timestamp: first),
            new CatchSpec(TimeSpan.Zero, Timestamp: second));

        // Act
        var proposal = Sut.Propose(batch).Single();

        // Assert
        proposal.ProposedStartedOn.Should().Be(first.LocalWallClock!.Value);
        proposal.ProposedStartedOn.Kind.Should().Be(DateTimeKind.Unspecified);
    }

    [Fact]
    public void ItShouldProduceMultipleIndependentClustersInChronologicalOrder()
    {
        // Arrange
        var batch = Batch(
            new CatchSpec(TimeSpan.Zero),
            new CatchSpec(TimeSpan.FromHours(1)),
            new CatchSpec(TimeSpan.FromHours(8)),
            new CatchSpec(TimeSpan.FromHours(9)));

        // Act
        var proposals = Sut.Propose(batch);

        // Assert
        proposals.Should().HaveCount(2);
        proposals[0].ProposedStartedOn.Should().BeBefore(proposals[1].ProposedStartedOn);
    }
}
