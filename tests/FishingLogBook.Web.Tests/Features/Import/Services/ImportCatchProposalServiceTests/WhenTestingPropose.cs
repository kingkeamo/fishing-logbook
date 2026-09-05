using AwesomeAssertions;
using FishingLogBook.Web.Features.Import.Enums;
using FishingLogBook.Web.Features.Import.Models;
using FishingLogBook.Web.Features.Import.Services;

namespace FishingLogBook.Web.Tests.Features.Import.Services.ImportCatchProposalServiceTests;

public class WhenTestingPropose : BaseImportCatchProposalServiceTest
{
    [Theory]
    [InlineData(299, 1)]
    [InlineData(300, 1)]
    [InlineData(301, 2)]
    public void ItShouldApplyTheInclusiveFiveMinuteThreshold(int gapSeconds, int expectedGroups)
    {
        // Arrange
        var first = Photo(0, ExplicitAt(TimeSpan.Zero));
        var second = Photo(1, ExplicitAt(TimeSpan.FromSeconds(gapSeconds)));

        // Act
        var proposals = Sut.Propose(Batch(first, second));

        // Assert
        ImportCatchProposalService.SameCatchTimeThreshold.Should().Be(TimeSpan.FromMinutes(5));
        proposals.Should().HaveCount(expectedGroups);
    }

    [Fact]
    public void ItShouldCreateOneSingletonForOneTrustworthyPhoto()
    {
        // Arrange
        var photo = Photo(0, ExplicitAt(TimeSpan.Zero));

        // Act
        var proposal = Sut.Propose(Batch(photo)).Single();

        // Assert
        proposal.PhotoIds.Should().Equal(photo.Id);
        proposal.CaughtOn.Should().Be(photo.Timestamp);
        proposal.Reasons.Should().Equal(ImportCatchProposalReasonEnum.TrustworthyCaptureTime);
    }

    [Fact]
    public void ItShouldGroupPhotographsWhenTheCompleteSpanIsWithinTheThreshold()
    {
        // Arrange
        var photos = new[]
        {
            Photo(0, ExplicitAt(TimeSpan.Zero)),
            Photo(1, ExplicitAt(TimeSpan.FromSeconds(90))),
            Photo(2, ExplicitAt(TimeSpan.FromMinutes(3)))
        };

        // Act
        var proposals = Sut.Propose(Batch(photos));

        // Assert
        proposals.Should().ContainSingle();
        proposals[0].PhotoIds.Should().Equal(photos.Select(photo => photo.Id));
    }

    [Fact]
    public void ItShouldNotChainWeakFallbackPhotographsBeyondTheFiveMinuteGroupSpan()
    {
        // Arrange
        var photos = new[]
        {
            Photo(0, ImportTimestampModel.FromWeakFallback(CapturedOn)),
            Photo(1, ImportTimestampModel.FromWeakFallback(CapturedOn.AddMinutes(4))),
            Photo(2, ImportTimestampModel.FromWeakFallback(CapturedOn.AddMinutes(8)))
        };

        // Act
        var proposals = Sut.Propose(Batch(photos));

        // Assert
        proposals.Select(proposal => proposal.PhotoIds.Count).Should().Equal(2, 1);
        proposals[0].PhotoIds.Should().Equal(photos[0].Id, photos[1].Id);
        proposals[1].PhotoIds.Should().Equal(photos[2].Id);
    }

    [Fact]
    public void ItShouldSplitWhenTheAdjacentGapExceedsTheThreshold()
    {
        // Arrange
        var photos = new[]
        {
            Photo(0, ExplicitAt(TimeSpan.Zero)),
            Photo(1, ExplicitAt(TimeSpan.FromMinutes(1))),
            Photo(2, ExplicitAt(TimeSpan.FromMinutes(7)))
        };

        // Act
        var proposals = Sut.Propose(Batch(photos));

        // Assert
        proposals.Select(proposal => proposal.PhotoIds.Count).Should().Equal(2, 1);
    }

    [Fact]
    public void ItShouldSortChronologicallyAndUseSelectionIndexForTies()
    {
        // Arrange
        var later = Photo(0, ExplicitAt(TimeSpan.FromMinutes(6)));
        var tiedSecond = Photo(2, ExplicitAt(TimeSpan.Zero));
        var tiedFirst = Photo(1, ExplicitAt(TimeSpan.Zero));

        // Act
        var proposals = Sut.Propose(Batch(later, tiedSecond, tiedFirst));

        // Assert
        proposals[0].PhotoIds.Should().Equal(tiedFirst.Id, tiedSecond.Id);
        proposals[1].PhotoIds.Should().Equal(later.Id);
    }

    [Theory]
    [MemberData(nameof(UnresolvedTimestamps))]
    public void ItShouldKeepUnresolvedTimestampsAsReviewableSingletons(
        ImportTimestampModel timestamp,
        ImportCatchProposalReasonEnum expectedReason)
    {
        // Arrange
        var first = Photo(0, timestamp);
        var second = Photo(1, timestamp);

        // Act
        var proposals = Sut.Propose(Batch(second, first));

        // Assert
        proposals.Should().HaveCount(2);
        proposals.SelectMany(proposal => proposal.PhotoIds).Should().Equal(first.Id, second.Id);
        proposals.Should().OnlyContain(proposal => proposal.CaughtOn == timestamp);
        proposals.Should().OnlyContain(proposal => proposal.Reasons.Contains(expectedReason));
    }

    [Fact]
    public void ItShouldGroupEquivalentInstantsWithDifferentOffsets()
    {
        // Arrange
        var first = Photo(0, ImportTimestampModel.FromExplicitInstant(
            DateTimeOffset.Parse("2025-06-14T10:00:00+01:00"),
            ImportTimestampSourceEnum.ExifOriginal));
        var second = Photo(1, ImportTimestampModel.FromExplicitInstant(
            DateTimeOffset.Parse("2025-06-14T05:00:00-04:00"),
            ImportTimestampSourceEnum.ExifDigitized));

        // Act
        var proposals = Sut.Propose(Batch(first, second));

        // Assert
        proposals.Should().ContainSingle();
        proposals[0].PhotoIds.Should().Equal(first.Id, second.Id);
    }

    [Fact]
    public void ItShouldGroupLocalExifTimesWithinFiveMinutesAndKeepThemReviewable()
    {
        // Arrange
        var capturedOn = new DateTime(2025, 6, 14, 9, 0, 0);
        var first = Photo(0, ImportTimestampModel.FromLocalWallClock(
            capturedOn,
            ImportTimestampSourceEnum.ExifOriginal));
        var second = Photo(1, ImportTimestampModel.FromLocalWallClock(
            capturedOn.AddMinutes(5),
            ImportTimestampSourceEnum.ExifDigitized));

        // Act
        var proposal = Sut.Propose(Batch(second, first)).Single();

        // Assert
        proposal.PhotoIds.Should().Equal(first.Id, second.Id);
        proposal.CaughtOn.Should().Be(first.Timestamp);
        proposal.Reasons.Should().Contain(ImportCatchProposalReasonEnum.AmbiguousTimestamp);
        proposal.ReviewStatus.Should().Be(ImportCatchReviewStatusEnum.Draft);
    }

    [Theory]
    [InlineData(300, 1)]
    [InlineData(301, 2)]
    public void ItShouldApplyTheFiveMinuteThresholdToWeakFallbackTimes(
        int gapSeconds,
        int expectedGroups)
    {
        // Arrange
        var first = Photo(0, ImportTimestampModel.FromWeakFallback(CapturedOn));
        var second = Photo(1, ImportTimestampModel.FromWeakFallback(CapturedOn.AddSeconds(gapSeconds)));

        // Act
        var proposals = Sut.Propose(Batch(second, first));

        // Assert
        proposals.Should().HaveCount(expectedGroups);
        proposals.SelectMany(proposal => proposal.PhotoIds).Should().Equal(first.Id, second.Id);
        proposals.Should().OnlyContain(proposal =>
            proposal.Reasons.Contains(ImportCatchProposalReasonEnum.WeakTimestamp)
            && proposal.ReviewStatus == ImportCatchReviewStatusEnum.Draft);
    }

    [Fact]
    public void ItShouldUseTheEarliestCompatibleLocationWithoutRequiringGps()
    {
        // Arrange
        var withoutGps = Photo(0, ExplicitAt(TimeSpan.Zero));
        var earliestGps = Photo(1, ExplicitAt(TimeSpan.FromSeconds(30)), 53.2707, -9.0570);
        var nearbyGps = Photo(2, ExplicitAt(TimeSpan.FromSeconds(60)), 53.2708, -9.0569);

        // Act
        var proposal = Sut.Propose(Batch(nearbyGps, withoutGps, earliestGps)).Single();

        // Assert
        proposal.PhotoIds.Should().Equal(withoutGps.Id, earliestGps.Id, nearbyGps.Id);
        proposal.Location.Should().Be(earliestGps.Location);
        proposal.Reasons.Should().NotContain(ImportCatchProposalReasonEnum.ConflictingGps);
    }

    [Fact]
    public void ItShouldRetainTimeGroupingButFlagMateriallyConflictingGps()
    {
        // Arrange
        var first = Photo(0, ExplicitAt(TimeSpan.Zero), 53.2707, -9.0568);
        var second = Photo(1, ExplicitAt(TimeSpan.FromMinutes(1)), 51.8985, -8.4756);

        // Act
        var proposal = Sut.Propose(Batch(first, second)).Single();

        // Assert
        proposal.PhotoIds.Should().Equal(first.Id, second.Id);
        proposal.Location.Should().BeNull();
        proposal.Reasons.Should().Contain(ImportCatchProposalReasonEnum.ConflictingGps);
    }

    [Fact]
    public void ItShouldIncludeEveryReadyActivePhotoExactlyOnceAndLeaveFailuresInTheBatch()
    {
        // Arrange
        var ready = Photo(0, ExplicitAt(TimeSpan.Zero));
        var failed = Photo(1, ImportTimestampModel.Missing(), ready: false);
        var removed = Photo(2, ExplicitAt(TimeSpan.FromMinutes(1)));
        removed.Remove();
        var batch = Batch(ready, failed, removed);

        // Act
        var proposals = Sut.Propose(batch);

        // Assert
        proposals.SelectMany(proposal => proposal.PhotoIds).Should().Equal(ready.Id);
        batch.Photos.Should().Contain(failed);
        failed.IsReady.Should().BeFalse();
    }

    [Fact]
    public void ItShouldProduceProposalsAcceptedByTheBatchMembershipInvariants()
    {
        // Arrange
        var photos = new[]
        {
            Photo(0, ExplicitAt(TimeSpan.Zero)),
            Photo(1, ExplicitAt(TimeSpan.FromMinutes(1))),
            Photo(2, ExplicitAt(TimeSpan.FromMinutes(5))),
            Photo(3, ImportTimestampModel.Missing())
        };
        var batch = Batch(photos);

        // Act
        var proposals = Sut.Propose(batch);
        foreach (var proposal in proposals)
        {
            batch.AddCatchProposal(proposal);
        }

        // Assert
        batch.CatchProposals.Should().Equal(proposals);
        batch.CatchProposals.SelectMany(proposal => proposal.PhotoIds)
            .Should().BeEquivalentTo(photos.Select(photo => photo.Id));
    }

    [Fact]
    public void ItShouldRepeatTheSameGroupingAndDerivedValues()
    {
        // Arrange
        var photos = new[]
        {
            Photo(2, ExplicitAt(TimeSpan.FromMinutes(5))),
            Photo(0, ExplicitAt(TimeSpan.Zero), 53.2707, -9.0570),
            Photo(1, ExplicitAt(TimeSpan.FromMinutes(1)), 53.2708, -9.0569),
            Photo(3, ImportTimestampModel.Missing())
        };
        var batch = Batch(photos);

        // Act
        var first = Sut.Propose(batch);
        var second = Sut.Propose(batch);

        // Assert
        first.Select(proposal => proposal.PhotoIds).Should().BeEquivalentTo(
            second.Select(proposal => proposal.PhotoIds),
            options => options.WithStrictOrdering());
        first.Select(proposal => proposal.CaughtOn).Should().Equal(second.Select(proposal => proposal.CaughtOn));
        first.Select(proposal => proposal.Location).Should().Equal(second.Select(proposal => proposal.Location));
        first.Select(proposal => proposal.Reasons).Should().BeEquivalentTo(
            second.Select(proposal => proposal.Reasons),
            options => options.WithStrictOrdering());
    }

    public static TheoryData<ImportTimestampModel, ImportCatchProposalReasonEnum> UnresolvedTimestamps => new()
    {
        { ImportTimestampModel.Missing(), ImportCatchProposalReasonEnum.MissingTimestamp },
        { ImportTimestampModel.Unusable(ImportTimestampSourceEnum.ExifOriginal), ImportCatchProposalReasonEnum.UnusableTimestamp }
    };
}
