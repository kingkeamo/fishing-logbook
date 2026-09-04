using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Import.Enums;

namespace FishingLogBook.Web.Tests.Features.Import.Models.ImportModelTests;

public class WhenTestingTripProposal : BaseImportModelTest
{
    [Fact]
    public void ItShouldRequireAnIdentityWhenUsingAnExistingTrip()
    {
        // Arrange
        var proposal = Trip();
        Action decide = () => proposal.Decide(ImportTripDecisionEnum.UseExisting);

        // Act
        var assertion = decide.Should();

        // Assert
        assertion.Throw<ArgumentException>();
    }

    [Fact]
    public void ItShouldKeepTheSelectedExistingTripIdentity()
    {
        // Arrange
        var proposal = Trip();
        var existingTripId = Guid.NewGuid();

        // Act
        proposal.Decide(ImportTripDecisionEnum.UseExisting, existingTripId);

        // Assert
        proposal.Decision.Should().Be(ImportTripDecisionEnum.UseExisting);
        proposal.ExistingTripId.Should().Be(existingTripId);
        proposal.IsDecisionComplete.Should().BeTrue();
    }

    [Theory]
    [InlineData(ImportTripDecisionEnum.CreateNew)]
    [InlineData(ImportTripDecisionEnum.NoTrip)]
    public void ItShouldNotAllowANewOrNoTripDecisionToCarryAnExistingIdentity(
        ImportTripDecisionEnum decision)
    {
        // Arrange
        var proposal = Trip();
        Action decide = () => proposal.Decide(decision, Guid.NewGuid());

        // Act
        var assertion = decide.Should();

        // Assert
        assertion.Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(ImportTripDecisionEnum.CreateNew)]
    [InlineData(ImportTripDecisionEnum.NoTrip)]
    public void ItShouldCompleteANewOrNoTripDecisionWithoutAnExistingIdentity(
        ImportTripDecisionEnum decision)
    {
        // Arrange
        var proposal = Trip();

        // Act
        proposal.Decide(decision);

        // Assert
        proposal.Decision.Should().Be(decision);
        proposal.ExistingTripId.Should().BeNull();
        proposal.IsDecisionComplete.Should().BeTrue();
    }

    [Fact]
    public void ItShouldClearAnExistingIdentityWhenTheDecisionChangesToNoTrip()
    {
        // Arrange
        var proposal = Trip();
        proposal.Decide(ImportTripDecisionEnum.UseExisting, Guid.NewGuid());

        // Act
        proposal.Decide(ImportTripDecisionEnum.NoTrip);

        // Assert
        proposal.Decision.Should().Be(ImportTripDecisionEnum.NoTrip);
        proposal.ExistingTripId.Should().BeNull();
    }

    [Fact]
    public void ItShouldKeepDistinctTransientParticipantsForANewTrip()
    {
        // Arrange
        var proposal = Trip();
        var first = new AnglerSummaryDto(Guid.NewGuid(), "Patrick", null, null, null);
        var second = new AnglerSummaryDto(Guid.NewGuid(), "Mark", null, null, null);
        proposal.Decide(ImportTripDecisionEnum.CreateNew);

        // Act
        proposal.AddParticipant(first);
        proposal.AddParticipant(second);
        proposal.AddParticipant(first);

        // Assert
        proposal.Participants.Should().Equal(first, second);
    }

    [Fact]
    public void ItShouldRemoveATransientParticipant()
    {
        // Arrange
        var proposal = Trip();
        var angler = new AnglerSummaryDto(Guid.NewGuid(), "Patrick", null, null, null);
        proposal.Decide(ImportTripDecisionEnum.CreateNew);
        proposal.AddParticipant(angler);

        // Act
        proposal.RemoveParticipant(angler.UserId);

        // Assert
        proposal.Participants.Should().BeEmpty();
        proposal.IsDecisionComplete.Should().BeTrue();
    }

    [Theory]
    [InlineData(ImportTripDecisionEnum.NoTrip)]
    [InlineData(ImportTripDecisionEnum.UseExisting)]
    public void ItShouldClearNewTripParticipantsWhenChangingDecision(ImportTripDecisionEnum decision)
    {
        // Arrange
        var proposal = Trip();
        proposal.Decide(ImportTripDecisionEnum.CreateNew);
        proposal.AddParticipant(new AnglerSummaryDto(Guid.NewGuid(), "Patrick", null, null, null));

        // Act
        proposal.Decide(decision, decision == ImportTripDecisionEnum.UseExisting ? Guid.NewGuid() : null);

        // Assert
        proposal.Participants.Should().BeEmpty();
    }
}
