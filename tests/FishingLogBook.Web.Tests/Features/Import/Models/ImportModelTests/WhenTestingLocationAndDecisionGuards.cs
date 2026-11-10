using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Import.Enums;
using FishingLogBook.Web.Features.Import.Models;

namespace FishingLogBook.Web.Tests.Features.Import.Models.ImportModelTests;

public class WhenTestingLocationAndDecisionGuards : BaseImportModelTest
{
    private static ImportTripProposalModel Proposal()
    {
        var start = new DateTime(2026, 8, 17, 8, 0, 0);
        return new ImportTripProposalModel(
            Guid.NewGuid(),
            [Guid.NewGuid()],
            ImportTripSuggestionConfidenceEnum.Strong,
            [],
            start,
            start.AddHours(1));
    }

    [Theory]
    [InlineData(null, 5.0)]
    [InlineData(5.0, null)]
    public void ItShouldRefuseALocationWithOnlyOneCoordinate(double? latitude, double? longitude)
    {
        // Arrange
        // Act
        var act = () => new ImportLocationModel(latitude, longitude, historicalGpsPresent: true);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("Latitude and longitude must be supplied together.");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ItShouldRefuseALocationWhoseGpsFlagContradictsItsCoordinates(bool coordinatesPresent)
    {
        // Arrange
        double? latitude = coordinatesPresent ? 53.1 : null;
        double? longitude = coordinatesPresent ? -6.2 : null;

        // Act
        var act = () => new ImportLocationModel(latitude, longitude, historicalGpsPresent: !coordinatesPresent);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("Historical GPS presence must match the coordinate state.");
    }

    [Fact]
    public void ItShouldRefuseToAcceptALocationWithoutCoordinates()
    {
        // Arrange
        // Act
        var act = () => new ImportLocationModel(
            null,
            null,
            historicalGpsPresent: false,
            ImportLocationDecisionEnum.Accepted);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("A location without coordinates cannot be accepted.");
    }

    [Fact]
    public void ItShouldRefuseAResolvedLookupWithoutAResult()
    {
        // Arrange
        // Act
        var act = () => new ImportLocationModel(
            53.1,
            -6.2,
            historicalGpsPresent: true,
            lookupStatus: ImportLocationLookupStatusEnum.Resolved,
            lookupResult: null);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("lookupResult");
    }

    [Fact]
    public void ItShouldDropTheResolvedPlaceNameWhenTheLookupIsReplacedByAFailure()
    {
        // Arrange
        var resolved = new ImportLocationModel(53.1, -6.2, true).WithLookup(
            ImportLocationLookupStatusEnum.Resolved,
            new ImportLocationLookupResultModel(["Galway", "Ireland"]) { Locality = "Galway", Country = "Ireland" });
        resolved.PlaceName.Should().Be("Galway, Ireland");

        // Act
        var failed = resolved.WithLookup(ImportLocationLookupStatusEnum.Failed);

        // Assert
        failed.PlaceName.Should().BeNull();
        failed.LookupStatus.Should().Be(ImportLocationLookupStatusEnum.Failed);
        failed.LookupResult.Should().BeNull();
        failed.Latitude.Should().Be(53.1);
    }

    [Fact]
    public void ItShouldNotBeDecidedUntilADecisionIsMade()
    {
        // Arrange
        var proposal = Proposal();

        // Act
        var before = proposal.IsDecisionComplete;
        proposal.Decide(ImportTripDecisionEnum.NoTrip);

        // Assert
        before.Should().BeFalse();
        proposal.IsDecisionComplete.Should().BeTrue();
    }

    [Fact]
    public void ItShouldTreatARemovedProposalAsDecidedEvenWithoutADecision()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        var start = new DateTime(2026, 8, 17, 8, 0, 0);
        var proposal = new ImportTripProposalModel(
            Guid.NewGuid(),
            [catchId],
            ImportTripSuggestionConfidenceEnum.Weak,
            [],
            start,
            start);

        // Act
        proposal.RemoveCatch(catchId);

        // Assert
        proposal.IsRemoved.Should().BeTrue();
        proposal.Decision.Should().Be(ImportTripDecisionEnum.Undecided);
        proposal.IsDecisionComplete.Should().BeTrue();
    }

    [Fact]
    public void ItShouldRefuseToUndecideAProposal()
    {
        // Arrange
        var proposal = Proposal();

        // Act
        var act = () => proposal.Decide(ImportTripDecisionEnum.Undecided);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("decision");
    }

    [Theory]
    [InlineData(ImportTripDecisionEnum.NoTrip)]
    [InlineData(ImportTripDecisionEnum.UseExisting)]
    public void ItShouldRefuseParticipantsUnlessTheDecisionIsToCreateANewTrip(ImportTripDecisionEnum decision)
    {
        // Arrange
        var proposal = Proposal();
        proposal.Decide(decision, decision == ImportTripDecisionEnum.UseExisting ? Guid.NewGuid() : null);

        // Act
        var act = () => proposal.AddParticipant(new AnglerSummaryDto(Guid.NewGuid(), "Mary", null, null, null));

        // Assert
        act.Should().Throw<InvalidOperationException>();
        proposal.Participants.Should().BeEmpty();
    }

    [Fact]
    public void ItShouldNotAddTheSameParticipantTwice()
    {
        // Arrange
        var proposal = Proposal();
        proposal.Decide(ImportTripDecisionEnum.CreateNew);
        var angler = new AnglerSummaryDto(Guid.NewGuid(), "Mary", null, null, null);

        // Act
        proposal.AddParticipant(angler);
        proposal.AddParticipant(angler);

        // Assert
        proposal.Participants.Should().ContainSingle().Which.Should().Be(angler);
    }
}
