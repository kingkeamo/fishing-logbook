using AwesomeAssertions;
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
}
