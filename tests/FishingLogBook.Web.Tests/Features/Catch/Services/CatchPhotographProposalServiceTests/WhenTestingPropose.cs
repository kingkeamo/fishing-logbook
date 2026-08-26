using AwesomeAssertions;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Photographs.Models;

namespace FishingLogBook.Web.Tests.Features.Catch.Services.CatchPhotographProposalServiceTests;

public class WhenTestingPropose : BaseCatchPhotographProposalServiceTest
{
    [Fact]
    public void ItShouldProposeNothingWhenNoPhotographsAreSelected()
    {
        // Arrange
        // Act
        var proposal = Sut.Propose([], Now);

        // Assert
        proposal.Should().Be(CatchPhotographProposalModel.Empty);
    }

    [Fact]
    public void ItShouldProposeNothingWhenNoPhotographCarriesMetadata()
    {
        // Arrange
        var photographs = new[] { PhotographMetadataModel.Empty, PhotographMetadataModel.Empty };

        // Act
        var proposal = Sut.Propose(photographs, Now);

        // Assert
        proposal.Should().Be(CatchPhotographProposalModel.Empty);
    }

    [Fact]
    public void ItShouldIgnoreACaptureDateInTheFutureButKeepItsCoordinates()
    {
        // Arrange
        var photographs = new[] { DatedAndLocated("2027-01-01T09:00:00Z", 53.2707, -9.0568) };

        // Act
        var proposal = Sut.Propose(photographs, Now);

        // Assert
        proposal.CaughtOn.Should().BeNull();
        proposal.HasConflictingDates.Should().BeFalse();
        proposal.Latitude.Should().Be(53.2707);
        proposal.Longitude.Should().Be(-9.0568);
        proposal.CoordinatesCapturedOn.Should().BeNull();
    }

    [Fact]
    public void ItShouldFlagConflictingDatesWhenPhotographsSpanDifferentDays()
    {
        // Arrange
        var photographs = new[]
        {
            Dated("2026-08-20T14:00:00Z"),
            Dated("2026-08-11T09:15:00Z")
        };

        // Act
        var proposal = Sut.Propose(photographs, Now);

        // Assert
        proposal.HasConflictingDates.Should().BeTrue();
        proposal.CaughtOn.Should().Be(DateTimeOffset.Parse("2026-08-11T09:15:00Z"));
    }

    [Fact]
    public void ItShouldFlagConflictingDatesWhenPhotographsAreHoursApartOnOneDay()
    {
        // Arrange
        var photographs = new[]
        {
            Dated("2026-08-20T07:00:00Z"),
            Dated("2026-08-20T13:30:00Z")
        };

        // Act
        var proposal = Sut.Propose(photographs, Now);

        // Assert
        proposal.HasConflictingDates.Should().BeTrue();
        proposal.CaughtOn.Should().Be(DateTimeOffset.Parse("2026-08-20T07:00:00Z"));
    }

    [Fact]
    public void ItShouldFlagConflictingCoordinatesWhenPhotographsAreKilometresApart()
    {
        // Arrange
        var photographs = new[]
        {
            DatedAndLocated("2026-08-20T07:00:00Z", 53.2707, -9.0568),
            DatedAndLocated("2026-08-20T07:05:00Z", 53.3200, -9.0568)
        };

        // Act
        var proposal = Sut.Propose(photographs, Now);

        // Assert
        proposal.HasConflictingCoordinates.Should().BeTrue();
        proposal.HasCoordinates.Should().BeFalse();
        proposal.Latitude.Should().BeNull();
        proposal.Longitude.Should().BeNull();
        proposal.HasConflictingDates.Should().BeFalse();
        proposal.CaughtOn.Should().Be(DateTimeOffset.Parse("2026-08-20T07:00:00Z"));
    }

    [Fact]
    public void ItShouldFlagBothConflictsWhenDatesAndCoordinatesDisagree()
    {
        // Arrange
        var photographs = new[]
        {
            DatedAndLocated("2026-08-20T07:00:00Z", 53.2707, -9.0568),
            DatedAndLocated("2026-08-14T15:10:00Z", 51.8985, -8.4756)
        };

        // Act
        var proposal = Sut.Propose(photographs, Now);

        // Assert
        proposal.HasConflictingDates.Should().BeTrue();
        proposal.HasConflictingCoordinates.Should().BeTrue();
        proposal.HasCoordinates.Should().BeFalse();
        proposal.Latitude.Should().BeNull();
        proposal.Longitude.Should().BeNull();
        proposal.CaughtOn.Should().Be(DateTimeOffset.Parse("2026-08-14T15:10:00Z"));
    }

    [Fact]
    public void ItShouldNotFlagPhotographsThatSimplyLackMetadata()
    {
        // Arrange
        var photographs = new[]
        {
            DatedAndLocated("2026-08-20T07:00:00Z", 53.2707, -9.0568),
            Dated("2026-08-20T07:01:30Z"),
            PhotographMetadataModel.Empty
        };

        // Act
        var proposal = Sut.Propose(photographs, Now);

        // Assert
        proposal.HasConflictingDates.Should().BeFalse();
        proposal.HasConflictingCoordinates.Should().BeFalse();
        proposal.CaughtOn.Should().Be(DateTimeOffset.Parse("2026-08-20T07:00:00Z"));
        proposal.Latitude.Should().Be(53.2707);
        proposal.Longitude.Should().Be(-9.0568);
    }

    [Fact]
    public void ItShouldProposeTheOnlyAvailableCaptureDateAndCoordinates()
    {
        // Arrange
        var photographs = new[]
        {
            PhotographMetadataModel.Empty,
            DatedAndLocated("2025-06-14T06:32:10Z", 53.2707, -9.0568),
            PhotographMetadataModel.Empty
        };

        // Act
        var proposal = Sut.Propose(photographs, Now);

        // Assert
        proposal.CaughtOn.Should().Be(DateTimeOffset.Parse("2025-06-14T06:32:10Z"));
        proposal.Latitude.Should().Be(53.2707);
        proposal.CoordinatesCapturedOn.Should().Be(DateTimeOffset.Parse("2025-06-14T06:32:10Z"));
        proposal.HasConflictingDates.Should().BeFalse();
        proposal.HasConflictingCoordinates.Should().BeFalse();
    }

    [Fact]
    public void ItShouldProposeTheEarliestCaptureDateForPhotographsOfOneFish()
    {
        // Arrange
        var photographs = new[]
        {
            Dated("2025-06-14T06:33:40Z"),
            Dated("2025-06-14T06:32:10Z"),
            Dated("2025-06-14T06:35:02Z")
        };

        // Act
        var proposal = Sut.Propose(photographs, Now);

        // Assert
        proposal.CaughtOn.Should().Be(DateTimeOffset.Parse("2025-06-14T06:32:10Z"));
        proposal.HasConflictingDates.Should().BeFalse();
    }

    [Fact]
    public void ItShouldTreatPhotographsWithinTheSameCatchWindowAsConsistent()
    {
        // Arrange
        var photographs = new[]
        {
            Dated("2025-06-14T06:32:10Z"),
            Dated("2025-06-14T06:57:10Z")
        };

        // Act
        var proposal = Sut.Propose(photographs, Now);

        // Assert
        proposal.HasConflictingDates.Should().BeFalse();
        proposal.CaughtOn.Should().Be(DateTimeOffset.Parse("2025-06-14T06:32:10Z"));
    }

    [Fact]
    public void ItShouldUseNearbyCoordinatesFromTheEarliestPhotographWithoutAveraging()
    {
        // Arrange
        var photographs = new[]
        {
            DatedAndLocated("2025-06-14T06:35:02Z", 53.2710, -9.0568),
            DatedAndLocated("2025-06-14T06:32:10Z", 53.2707, -9.0570)
        };

        // Act
        var proposal = Sut.Propose(photographs, Now);

        // Assert
        proposal.HasConflictingCoordinates.Should().BeFalse();
        proposal.Latitude.Should().Be(53.2707);
        proposal.Longitude.Should().Be(-9.0570);
        proposal.CoordinatesCapturedOn.Should().Be(DateTimeOffset.Parse("2025-06-14T06:32:10Z"));
    }

    [Fact]
    public void ItShouldProposeTheSameResultRegardlessOfSelectionOrder()
    {
        // Arrange
        var photographs = new[]
        {
            DatedAndLocated("2025-06-14T06:35:02Z", 53.2710, -9.0568),
            Located(53.2708, -9.0569),
            DatedAndLocated("2025-06-14T06:32:10Z", 53.2707, -9.0570)
        };

        // Act
        var proposal = Sut.Propose(photographs, Now);
        var reversed = Sut.Propose([.. photographs.Reverse()], Now);

        // Assert
        proposal.Should().Be(reversed);
        proposal.Latitude.Should().Be(53.2707);
        proposal.CaughtOn.Should().Be(DateTimeOffset.Parse("2025-06-14T06:32:10Z"));
    }

    [Fact]
    public void ItShouldFallBackToSelectionOrderWhenNoLocatedPhotographIsDated()
    {
        // Arrange
        var photographs = new[]
        {
            Located(53.2707, -9.0570),
            Located(53.2710, -9.0568)
        };

        // Act
        var proposal = Sut.Propose(photographs, Now);

        // Assert
        proposal.Latitude.Should().Be(53.2707);
        proposal.Longitude.Should().Be(-9.0570);
        proposal.CoordinatesCapturedOn.Should().BeNull();
        proposal.HasConflictingCoordinates.Should().BeFalse();
    }

    [Fact]
    public void ItShouldTreatACompatibleFileTimestampAsOneCatchWithoutWarning()
    {
        // Arrange
        var photographs = new[]
        {
            Dated("2026-08-22T10:28:00Z"),
            FileDated("2026-08-22T10:29:00Z")
        };

        // Act
        var proposal = Sut.Propose(photographs, Now);

        // Assert
        proposal.HasConflictingDates.Should().BeFalse();
        proposal.CaughtOn.Should().Be(DateTimeOffset.Parse("2026-08-22T10:28:00Z"));
    }

    [Fact]
    public void ItShouldNotLetAnIncompatibleFileTimestampOverruleTheExifDate()
    {
        // Arrange
        var photographs = new[]
        {
            Dated("2026-08-22T10:28:00Z"),
            FileDated("2026-08-10T09:00:00Z")
        };

        // Act
        var proposal = Sut.Propose(photographs, Now);

        // Assert
        proposal.HasConflictingDates.Should().BeFalse();
        proposal.CaughtOn.Should().Be(DateTimeOffset.Parse("2026-08-22T10:28:00Z"));
    }

    [Fact]
    public void ItShouldFlagAConflictBetweenFileTimestampsWhenNoPhotographCarriesExifEvidence()
    {
        // Arrange
        var photographs = new[]
        {
            FileDated("2026-08-22T10:28:00Z"),
            FileDated("2026-08-10T09:00:00Z")
        };

        // Act
        var proposal = Sut.Propose(photographs, Now);

        // Assert
        proposal.HasConflictingDates.Should().BeTrue();
        proposal.CaughtOn.Should().Be(DateTimeOffset.Parse("2026-08-10T09:00:00Z"));
    }

    [Fact]
    public void ItShouldNotFlagAConflictWhenOnlyAMetadatalessPhotographCarriesAFileTimestamp()
    {
        // Arrange
        var photographs = new[]
        {
            DatedAndLocated("2026-08-22T10:28:00Z", 53.2707, -9.0568),
            Dated("2026-08-22T10:29:30Z"),
            FileDated("2026-08-25T11:59:00Z")
        };

        // Act
        var proposal = Sut.Propose(photographs, Now);

        // Assert
        proposal.HasConflictingDates.Should().BeFalse();
        proposal.HasConflictingCoordinates.Should().BeFalse();
        proposal.CaughtOn.Should().Be(DateTimeOffset.Parse("2026-08-22T10:28:00Z"));
        proposal.Latitude.Should().Be(53.2707);
    }

    [Fact]
    public void ItShouldPreferTheExifDateOverAnEarlierFileTimestamp()
    {
        // Arrange
        var photographs = new[]
        {
            FileDated("2026-08-22T10:28:00Z"),
            Dated("2026-08-22T10:28:30Z")
        };

        // Act
        var proposal = Sut.Propose(photographs, Now);

        // Assert
        proposal.HasConflictingDates.Should().BeFalse();
        proposal.CaughtOn.Should().Be(DateTimeOffset.Parse("2026-08-22T10:28:30Z"));
    }

    [Fact]
    public void ItShouldUseTheEarliestFileTimestampWhenNoPhotographCarriesExifEvidence()
    {
        // Arrange
        var photographs = new[]
        {
            FileDated("2026-08-22T10:29:00Z"),
            FileDated("2026-08-22T10:28:43Z")
        };

        // Act
        var proposal = Sut.Propose(photographs, Now);

        // Assert
        proposal.CaughtOn.Should().Be(DateTimeOffset.Parse("2026-08-22T10:28:43Z"));
        proposal.HasConflictingDates.Should().BeFalse();
    }

    [Fact]
    public void ItShouldIgnoreAFileTimestampBeyondTheAllowedFutureSkew()
    {
        // Arrange
        var photographs = new[] { FileDated("2026-08-25T13:00:00Z") };

        // Act
        var proposal = Sut.Propose(photographs, Now);

        // Assert
        proposal.CaughtOn.Should().BeNull();
        proposal.HasConflictingDates.Should().BeFalse();
    }
}
