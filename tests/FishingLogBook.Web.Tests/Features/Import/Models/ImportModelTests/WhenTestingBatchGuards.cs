using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Import.Enums;
using FishingLogBook.Web.Features.Import.Models;

namespace FishingLogBook.Web.Tests.Features.Import.Models.ImportModelTests;

public class WhenTestingBatchGuards : BaseImportModelTest
{
    private static readonly Guid MissingId = Guid.Parse("99999999-9999-9999-9999-999999999999");

    [Fact]
    public void ItShouldRequireABatchIdentity()
    {
        // Arrange
        // Act
        var act = () => new ImportBatchModel(Guid.Empty, Method(), Species());

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ItShouldNotBeReadyForConfirmationWithInvalidCatalogueSelections()
    {
        // Arrange
        var batch = Batch(method: new ImportCatalogueSelectionModel(Guid.Empty, string.Empty, string.Empty));

        // Act
        var act = () => batch.ValidateForPersistence(DateTimeOffset.UtcNow);

        // Assert
        batch.IsReadyForConfirmation.Should().BeFalse();
        act.Should().Throw<InvalidOperationException>().WithMessage("*valid catalogue selections*");
    }

    [Fact]
    public void ItShouldNotPersistABatchWithoutPhotographsAndCatches()
    {
        // Arrange
        var batch = Batch();

        // Act
        var act = () => batch.ValidateForPersistence(DateTimeOffset.UtcNow);

        // Assert
        batch.IsReadyForConfirmation.Should().BeFalse();
        act.Should().Throw<InvalidOperationException>().WithMessage("*active photographs and Catches*");
    }

    [Fact]
    public void ItShouldRejectAPhotoWithADuplicateIdentityOrSelectionOrder()
    {
        // Arrange
        var batch = Batch();
        batch.AddPhoto(Photo(PhotoId, 0));

        // Act
        var sameId = () => batch.AddPhoto(Photo(PhotoId, 1));
        var sameOrder = () => batch.AddPhoto(Photo(SecondPhotoId, 0));

        // Assert
        sameId.Should().Throw<InvalidOperationException>();
        sameOrder.Should().Throw<InvalidOperationException>();
        batch.Photos.Should().ContainSingle();
    }

    [Fact]
    public void ItShouldNotBeAbleToProcessPhotosOnceCancelled()
    {
        // Arrange
        var batch = Batch();
        batch.BeginPhotoProcessing();

        // Act
        batch.Cancel();
        var begin = batch.BeginPhotoProcessing;

        // Assert
        batch.IsCancelled.Should().BeTrue();
        batch.IsProcessingPhotos.Should().BeFalse();
        batch.CanProcessPhotos.Should().BeFalse();
        begin.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ItShouldRejectADuplicateCatchProposalIdentity()
    {
        // Arrange
        var batch = Batch();
        batch.AddPhoto(Photo());
        batch.AddCatchProposal(Catch());

        // Act
        var act = () => batch.AddCatchProposal(Catch(CatchId, [SecondPhotoId]));

        // Assert
        act.Should().Throw<InvalidOperationException>();
        batch.CatchProposals.Should().ContainSingle();
    }

    [Fact]
    public void ItShouldRejectACatchProposalThatReferencesAPhotoThatIsNotSelected()
    {
        // Arrange
        var batch = Batch();
        batch.AddPhoto(Photo());

        // Act
        var act = () => batch.AddCatchProposal(Catch(CatchId, [SecondPhotoId]));

        // Assert
        act.Should().Throw<InvalidOperationException>();
        batch.CatchProposals.Should().BeEmpty();
    }

    [Fact]
    public void ItShouldRejectADuplicateIdentityWhenReplacingCatchProposals()
    {
        // Arrange
        var batch = Batch();
        batch.AddPhoto(Photo(PhotoId, 0));
        batch.AddPhoto(Photo(SecondPhotoId, 1));

        // Act
        var act = () => batch.ReplaceCatchProposals([Catch(CatchId, [PhotoId]), Catch(CatchId, [SecondPhotoId])]);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ItShouldRejectAnUnknownPhotoWhenReplacingCatchProposals()
    {
        // Arrange
        var batch = Batch();
        batch.AddPhoto(Photo());

        // Act
        var act = () => batch.ReplaceCatchProposals([Catch(CatchId, [SecondPhotoId])]);

        // Assert
        act.Should().Throw<InvalidOperationException>();
        batch.CatchProposals.Should().BeEmpty();
    }

    [Fact]
    public void ItShouldRejectAPhotoSharedByTwoReplacementCatchProposals()
    {
        // Arrange
        var batch = Batch();
        batch.AddPhoto(Photo());

        // Act
        var act = () => batch.ReplaceCatchProposals([Catch(CatchId, [PhotoId]), Catch(SecondCatchId, [PhotoId])]);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ItShouldRejectADuplicateTripProposalIdentity()
    {
        // Arrange
        var batch = ReviewedBatch();
        batch.AddTripProposal(Trip());

        // Act
        var act = () => batch.AddTripProposal(Trip());

        // Assert
        act.Should().Throw<InvalidOperationException>();
        batch.TripProposals.Should().ContainSingle();
    }

    [Fact]
    public void ItShouldRejectATripProposalForACatchThatIsNotReviewed()
    {
        // Arrange
        var batch = Batch();
        batch.AddPhoto(Photo());
        batch.AddCatchProposal(Catch(caughtOn: ImportTimestampModel.Missing()));

        // Act
        var act = () => batch.AddTripProposal(Trip());

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ItShouldRefuseTripChangesForATripThatDoesNotExist()
    {
        // Arrange
        var batch = ReviewedBatch();

        // Act
        var decide = () => batch.DecideTrip(MissingId, ImportTripDecisionEnum.CreateNew);
        var removeCatch = () => batch.RemoveCatchFromTrip(MissingId, CatchId);
        var addParticipant = () => batch.AddTripParticipant(
            MissingId,
            new AnglerSummaryDto(Guid.NewGuid(), "Sam", null, null, null));
        var removeParticipant = () => batch.RemoveTripParticipant(MissingId, Guid.NewGuid());

        // Assert
        decide.Should().Throw<InvalidOperationException>();
        removeCatch.Should().Throw<InvalidOperationException>();
        addParticipant.Should().Throw<InvalidOperationException>();
        removeParticipant.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ItShouldIgnoreRemovingAPhotoOrCatchThatIsNotInTheBatch()
    {
        // Arrange
        var batch = ReviewedBatch();

        // Act
        batch.RemovePhoto(MissingId);
        batch.RemoveCatchProposal(MissingId);

        // Assert
        batch.Photos.Should().ContainSingle();
        batch.CatchProposals.Should().ContainSingle();
    }

    [Fact]
    public void ItShouldRefuseEveryCatchEditForACatchThatIsNotActive()
    {
        // Arrange
        var batch = ReviewedBatch();

        // Act
        var actions = new Action[]
        {
            () => batch.SetCatchCaughtOn(MissingId, ImportTimestampModel.UserConfirmed(CapturedOn)),
            () => batch.SetCatchMethod(MissingId, Method()),
            () => batch.SetCatchSpecies(MissingId, Species()),
            () => batch.SetCatchLocation(MissingId, null),
            () => batch.SetCatchWeight(MissingId, 1m),
            () => batch.SetCatchLength(MissingId, 1m),
            () => batch.MarkCatchReviewed(MissingId),
            () => batch.ConfirmDisplayedCatch(MissingId)
        };

        // Assert
        foreach (var action in actions)
        {
            action.Should().Throw<InvalidOperationException>();
        }
    }

    [Fact]
    public void ItShouldRefuseASplitOrMergeInvolvingAnUnknownCatch()
    {
        // Arrange
        var batch = ReviewedBatch();

        // Act
        var split = () => batch.SplitCatch(MissingId, [PhotoId], Guid.NewGuid());
        var mergeSelf = () => batch.MergeCatches(CatchId, CatchId);
        var mergeUnknown = () => batch.MergeCatches(CatchId, MissingId);

        // Assert
        split.Should().Throw<InvalidOperationException>();
        mergeSelf.Should().Throw<InvalidOperationException>();
        mergeUnknown.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ItShouldRecordTheStageAndReplacedDefaults()
    {
        // Arrange
        var batch = Batch();
        var other = new ImportCatalogueSelectionModel(Guid.NewGuid(), "Spin", "Spinning");

        // Act
        batch.SetStage(ImportStageEnum.ReviewCatches);
        batch.SetDefaults(other, Species());

        // Assert
        batch.Stage.Should().Be(ImportStageEnum.ReviewCatches);
        batch.FishingMethod.Should().Be(other);
    }

    private static ImportBatchModel ReviewedBatch()
    {
        var batch = Batch();
        batch.AddPhoto(Photo());
        batch.AddCatchProposal(Catch());
        batch.MarkCatchReviewed(CatchId);
        return batch;
    }
}
