using AwesomeAssertions;
using FishingLogBook.Web.Features.Import.Enums;
using FishingLogBook.Web.Features.Import.Models;

namespace FishingLogBook.Web.Tests.Features.Import.Models.ImportModelTests;

public class WhenTestingCatchProposal : BaseImportModelTest
{
    [Fact]
    public void ItShouldUseBatchDefaultsUntilOverridesAreSelected()
    {
        // Arrange
        var proposal = Catch();
        var methodOverride = new ImportCatalogueSelectionModel(Guid.NewGuid(), "Lure", "Lure");
        var speciesOverride = new ImportCatalogueSelectionModel(Guid.NewGuid(), "Pike", "Pike");

        // Act
        proposal.OverrideMethod(methodOverride);
        proposal.OverrideSpecies(speciesOverride);

        // Assert
        proposal.InheritedMethod.Should().Be(Method());
        proposal.InheritedSpecies.Should().Be(Species());
        proposal.Method.Should().Be(methodOverride);
        proposal.Species.Should().Be(speciesOverride);
    }

    [Fact]
    public void ItShouldRejectDuplicatePhotoMembership()
    {
        // Arrange
        Action create = () => _ = Catch(photoIds: [PhotoId, PhotoId]);

        // Act
        var assertion = create.Should();

        // Assert
        assertion.Throw<ArgumentException>();
    }

    [Fact]
    public void ItShouldRemainUnreadyUntilTheDateIsResolvedAndReviewCompletes()
    {
        // Arrange
        var proposal = Catch(caughtOn: ImportTimestampModel.Missing());

        // Act
        Action review = proposal.MarkReviewed;

        // Assert
        review.Should().Throw<InvalidOperationException>();
        proposal.IsReadyForConfirmation.Should().BeFalse();
        proposal.SetCaughtOn(ImportTimestampModel.UserConfirmed(CapturedOn));
        proposal.MarkReviewed();
        proposal.IsReadyForConfirmation.Should().BeTrue();
    }

    [Fact]
    public void ItShouldBecomeRemovedWhenItsLastPhotoIsRemoved()
    {
        // Arrange
        var proposal = Catch();

        // Act
        proposal.RemovePhoto(PhotoId);

        // Assert
        proposal.PhotoIds.Should().BeEmpty();
        proposal.IsRemoved.Should().BeTrue();
        proposal.IsReadyForConfirmation.Should().BeFalse();
    }

    [Fact]
    public void ItShouldAllowMissingGpsButRequireADecisionForAvailableGps()
    {
        // Arrange
        var withoutGps = Catch();
        var withGps = new ImportCatchProposalModel(
            Guid.NewGuid(), [PhotoId], ImportTimestampModel.UserConfirmed(CapturedOn), Method(), Species(),
            new ImportLocationModel(53.3498, -6.2603, true));

        // Act
        withoutGps.MarkReviewed();
        Action reviewWithUndecidedGps = withGps.MarkReviewed;

        // Assert
        withoutGps.IsReadyForConfirmation.Should().BeTrue();
        reviewWithUndecidedGps.Should().Throw<InvalidOperationException>();

        // Act
        withGps.SetLocation(withGps.Location!.Accept());
        withGps.MarkReviewed();

        // Assert
        withGps.IsReadyForConfirmation.Should().BeTrue();
    }

    [Fact]
    public void ItShouldRequireExplicitResolutionOfConflictingGps()
    {
        // Arrange
        var proposal = new ImportCatchProposalModel(
            Guid.NewGuid(), [PhotoId], ImportTimestampModel.UserConfirmed(CapturedOn), Method(), Species(),
            reasons: [ImportCatchProposalReasonEnum.ConflictingGps]);
        Action review = proposal.MarkReviewed;

        // Act
        var assertion = review.Should();

        // Assert
        assertion.Throw<InvalidOperationException>();
        proposal.HasUnresolvedGpsConflict.Should().BeTrue();
    }

    [Fact]
    public void ItShouldTreatConfirmingTheDisplayedFallbackAsExplicitTimestampApproval()
    {
        // Arrange
        var proposal = Catch(caughtOn: ImportTimestampModel.FromWeakFallback(CapturedOn));

        // Act
        proposal.ConfirmDisplayedValues();

        // Assert
        proposal.CaughtOn.State.Should().Be(ImportTimestampStateEnum.UserConfirmed);
        proposal.CaughtOn.Instant.Should().Be(CapturedOn);
        proposal.ReviewStatus.Should().Be(ImportCatchReviewStatusEnum.Reviewed);
        proposal.IsReadyForConfirmation.Should().BeTrue();
    }
}
