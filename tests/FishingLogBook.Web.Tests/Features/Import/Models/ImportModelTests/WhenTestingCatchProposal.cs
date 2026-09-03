using AwesomeAssertions;
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
        proposal.MarkReviewed();

        // Assert
        proposal.IsReadyForConfirmation.Should().BeFalse();
        proposal.SetCaughtOn(ImportTimestampModel.UserConfirmed(CapturedOn));
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
}
