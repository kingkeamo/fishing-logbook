using AwesomeAssertions;
using FishingLogBook.Web.Features.Import.Enums;
using FishingLogBook.Web.Features.Import.Models;

namespace FishingLogBook.Web.Tests.Features.Import.Models.ImportModelTests;

public class WhenTestingBatch : BaseImportModelTest
{
    [Fact]
    public void ItShouldRequireValidDefaultsBeforePhotoProcessing()
    {
        // Arrange
        var invalidMethod = new ImportCatalogueSelectionModel(Guid.Empty, string.Empty, string.Empty);
        var batch = Batch(method: invalidMethod);
        Action begin = batch.BeginPhotoProcessing;

        // Act
        var assertion = begin.Should();

        // Assert
        batch.CanProcessPhotos.Should().BeFalse();
        assertion.Throw<InvalidOperationException>();
    }

    [Fact]
    public void ItShouldRetainDeterministicPhotoOrder()
    {
        // Arrange
        var batch = Batch();

        // Act
        batch.AddPhoto(Photo(SecondPhotoId, 1));
        batch.AddPhoto(Photo(PhotoId, 0));

        // Assert
        batch.Photos.Select(photo => photo.Id).Should().Equal(PhotoId, SecondPhotoId);
    }

    [Fact]
    public void ItShouldPreventAPhotoBelongingToMultipleCatches()
    {
        // Arrange
        var batch = Batch();
        batch.AddPhoto(Photo());
        batch.AddCatchProposal(Catch());
        Action addAgain = () => batch.AddCatchProposal(Catch(SecondCatchId));

        // Act
        var assertion = addAgain.Should();

        // Assert
        assertion.Throw<InvalidOperationException>();
        batch.CatchProposals.Should().ContainSingle();
    }

    [Fact]
    public void ItShouldReplaceCatchProposalsWithoutRetainingOldMemberships()
    {
        // Arrange
        var batch = Batch();
        batch.AddPhoto(ReadyPersistencePhoto());
        batch.AddPhoto(ReadyPersistencePhoto(SecondPhotoId, 1));
        batch.AddCatchProposal(Catch());
        var replacement = Catch(SecondCatchId, [PhotoId, SecondPhotoId]);

        // Act
        batch.ReplaceCatchProposals([replacement]);

        // Assert
        batch.CatchProposals.Should().Equal(replacement);
    }

    [Fact]
    public void ItShouldRejectInvalidReplacementWithoutDiscardingCurrentProposals()
    {
        // Arrange
        var batch = Batch();
        var current = Catch();
        batch.AddPhoto(ReadyPersistencePhoto());
        batch.AddCatchProposal(current);
        var invalid = Catch(SecondCatchId, [Guid.NewGuid()]);
        Action replace = () => batch.ReplaceCatchProposals([invalid]);

        // Act
        var assertion = replace.Should();

        // Assert
        assertion.Throw<InvalidOperationException>();
        batch.CatchProposals.Should().Equal(current);
    }

    [Fact]
    public void ItShouldAllowTripProposalsToReferenceReviewedCatchesOnly()
    {
        // Arrange
        var batch = Batch();
        batch.AddPhoto(ReadyPersistencePhoto());
        batch.AddCatchProposal(Catch());
        Action addTrip = () => batch.AddTripProposal(Trip());

        // Act
        var assertion = addTrip.Should();

        // Assert
        assertion.Throw<InvalidOperationException>();
        batch.TripProposals.Should().BeEmpty();
    }

    [Fact]
    public void ItShouldPreventACatchBelongingToMultipleTripProposals()
    {
        // Arrange
        var batch = Batch();
        var catchProposal = Catch();
        catchProposal.MarkReviewed();
        batch.AddPhoto(Photo());
        batch.AddCatchProposal(catchProposal);
        batch.AddTripProposal(Trip());
        Action addAgain = () => batch.AddTripProposal(Trip(Guid.NewGuid()));

        // Act
        var assertion = addAgain.Should();

        // Assert
        assertion.Throw<InvalidOperationException>();
        batch.TripProposals.Should().ContainSingle();
    }

    [Fact]
    public void ItShouldRemoveAPhotoFromItsCatchAndTripMembership()
    {
        // Arrange
        var batch = Batch();
        var catchProposal = Catch();
        catchProposal.MarkReviewed();
        batch.AddPhoto(Photo());
        batch.AddCatchProposal(catchProposal);
        var tripProposal = Trip();
        batch.AddTripProposal(tripProposal);

        // Act
        batch.RemovePhoto(PhotoId);

        // Assert
        batch.Photos.Single().IsRemoved.Should().BeTrue();
        catchProposal.IsRemoved.Should().BeTrue();
        tripProposal.CatchProposalIds.Should().BeEmpty();
        tripProposal.IsRemoved.Should().BeTrue();
    }

    [Fact]
    public void ItShouldRemoveACatchFromItsTripMembership()
    {
        // Arrange
        var batch = Batch();
        var catchProposal = Catch();
        catchProposal.MarkReviewed();
        batch.AddPhoto(Photo());
        batch.AddCatchProposal(catchProposal);
        var tripProposal = Trip();
        batch.AddTripProposal(tripProposal);

        // Act
        batch.RemoveCatchProposal(CatchId);

        // Assert
        catchProposal.IsRemoved.Should().BeTrue();
        batch.Photos.Single().IsRemoved.Should().BeTrue();
        batch.TripProposals.Should().BeEmpty();
    }

    [Fact]
    public void ItShouldRequireReviewedResolvedCatchesAndTripDecisionsForConfirmation()
    {
        // Arrange
        var batch = Batch();
        var catchProposal = Catch(caughtOn: ImportTimestampModel.Missing());
        batch.AddPhoto(ReadyPersistencePhoto());
        batch.AddCatchProposal(catchProposal);

        // Act
        Action review = catchProposal.MarkReviewed;

        // Assert
        review.Should().Throw<InvalidOperationException>();
        batch.IsReadyForConfirmation.Should().BeFalse();
        catchProposal.SetCaughtOn(ImportTimestampModel.UserConfirmed(CapturedOn));
        catchProposal.MarkReviewed();
        batch.IsReadyForConfirmation.Should().BeTrue();
    }

    [Fact]
    public void ItShouldRejectAnUnassignedActivePhotoAtThePersistenceBoundary()
    {
        // Arrange
        var batch = Batch();
        var catchProposal = Catch();
        catchProposal.MarkReviewed();
        batch.AddPhoto(ReadyPersistencePhoto());
        batch.AddPhoto(ReadyPersistencePhoto(SecondPhotoId, 1));
        batch.AddCatchProposal(catchProposal);
        Action validate = () => batch.ValidateForPersistence(CapturedOn.AddDays(1));

        // Act
        var assertion = validate.Should();

        // Assert
        assertion.Throw<InvalidOperationException>()
            .WithMessage("*exactly one active Catch*");
    }

    [Fact]
    public void ItShouldRejectAFutureCatchAtThePersistenceBoundary()
    {
        // Arrange
        var now = CapturedOn;
        var batch = Batch();
        var catchProposal = Catch(caughtOn: ImportTimestampModel.UserConfirmed(now.AddTicks(1)));
        catchProposal.MarkReviewed();
        batch.AddPhoto(ReadyPersistencePhoto());
        batch.AddCatchProposal(catchProposal);
        Action validate = () => batch.ValidateForPersistence(now);

        // Act
        var assertion = validate.Should();

        // Assert
        assertion.Throw<InvalidOperationException>()
            .WithMessage("*valid historical timestamp*");
    }

    [Fact]
    public void ItShouldAcceptACompleteBatchAtThePersistenceBoundary()
    {
        // Arrange
        var batch = Batch();
        var catchProposal = Catch();
        catchProposal.MarkReviewed();
        batch.AddPhoto(ReadyPersistencePhoto());
        batch.AddCatchProposal(catchProposal);

        // Act
        var action = () => batch.ValidateForPersistence(CapturedOn.AddTicks(1));

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void ItShouldBlockPersistenceUntilAnExactDuplicateIsExplicitlyKept()
    {
        // Arrange
        var batch = Batch();
        var first = ReadyPersistencePhoto();
        var duplicate = ReadyPersistencePhoto(SecondPhotoId, 1);
        duplicate.SetDuplicateState(
            ImportDuplicateStatusEnum.Duplicate,
            ImportDuplicateReasonEnum.IdenticalPreparedBytes,
            [first.Id]);
        var catchProposal = Catch(photoIds: [first.Id, duplicate.Id]);
        catchProposal.MarkReviewed();
        batch.AddPhoto(first);
        batch.AddPhoto(duplicate);
        batch.AddCatchProposal(catchProposal);

        // Act
        var beforeDecision = batch.IsReadyForConfirmation;
        batch.KeepDuplicatePhoto(duplicate.Id);

        // Assert
        beforeDecision.Should().BeFalse();
        duplicate.DuplicateDecision.Should().Be(ImportDuplicateDecisionEnum.KeepBoth);
        batch.IsReadyForConfirmation.Should().BeTrue();
        batch.Photos.Should().OnlyContain(photo => !photo.IsRemoved);
    }

    [Fact]
    public void ItShouldResolveAnExactDuplicateThroughTheNormalRemovalFlow()
    {
        // Arrange
        var batch = Batch();
        var first = ReadyPersistencePhoto();
        var duplicate = ReadyPersistencePhoto(SecondPhotoId, 1);
        duplicate.SetDuplicateState(
            ImportDuplicateStatusEnum.Duplicate,
            ImportDuplicateReasonEnum.IdenticalPreparedBytes,
            [first.Id]);
        var catchProposal = Catch(photoIds: [first.Id, duplicate.Id]);
        catchProposal.MarkReviewed();
        batch.AddPhoto(first);
        batch.AddPhoto(duplicate);
        batch.AddCatchProposal(catchProposal);
        var trip = Trip();
        batch.AddTripProposal(trip);

        // Act
        batch.RemovePhoto(duplicate.Id);

        // Assert
        duplicate.IsRemoved.Should().BeTrue();
        catchProposal.PhotoIds.Should().Equal(first.Id);
        catchProposal.ReviewStatus.Should().Be(ImportCatchReviewStatusEnum.Draft);
        batch.TripProposals.Should().BeEmpty();
        batch.IsReadyForConfirmation.Should().BeFalse();
    }

    [Fact]
    public void ItShouldNotRequireAResolutionWhenTheDuplicateCounterpartWasRemoved()
    {
        // Arrange
        var batch = Batch();
        var first = ReadyPersistencePhoto();
        var duplicate = ReadyPersistencePhoto(SecondPhotoId, 1);
        duplicate.SetDuplicateState(
            ImportDuplicateStatusEnum.Duplicate,
            ImportDuplicateReasonEnum.IdenticalPreparedBytes,
            [first.Id]);
        batch.AddPhoto(first);
        batch.AddPhoto(duplicate);

        // Act
        batch.RemovePhoto(first.Id);

        // Assert
        duplicate.RequiresDuplicateDecision.Should().BeTrue();
        batch.HasUnresolvedExactDuplicates.Should().BeFalse();
    }

    [Fact]
    public void ItShouldRepresentMultipleIndependentTripProposals()
    {
        // Arrange
        var batch = Batch();
        var firstCatch = Catch();
        var secondCatch = Catch(SecondCatchId, [SecondPhotoId]);
        firstCatch.MarkReviewed();
        secondCatch.MarkReviewed();
        batch.AddPhoto(Photo());
        batch.AddPhoto(Photo(SecondPhotoId, 1));
        batch.AddCatchProposal(firstCatch);
        batch.AddCatchProposal(secondCatch);

        // Act
        batch.AddTripProposal(Trip(catchIds: [CatchId]));
        batch.AddTripProposal(Trip(Guid.NewGuid(), [SecondCatchId]));

        // Assert
        batch.TripProposals.Should().HaveCount(2);
        batch.TripProposals.SelectMany(proposal => proposal.CatchProposalIds)
            .Should().BeEquivalentTo([CatchId, SecondCatchId]);
    }

    [Fact]
    public void ItShouldSplitSelectedPhotosIntoAnOrderedNewCatchAndInvalidateTrips()
    {
        // Arrange
        var batch = Batch();
        batch.AddPhoto(Photo(PhotoId, 0));
        batch.AddPhoto(Photo(SecondPhotoId, 1));
        var source = Catch(photoIds: [PhotoId, SecondPhotoId]);
        source.MarkReviewed();
        batch.AddCatchProposal(source);
        batch.AddTripProposal(Trip());

        // Act
        var created = batch.SplitCatch(CatchId, [SecondPhotoId], SecondCatchId);

        // Assert
        source.PhotoIds.Should().Equal(PhotoId);
        source.ReviewStatus.Should().Be(ImportCatchReviewStatusEnum.Draft);
        created.PhotoIds.Should().Equal(SecondPhotoId);
        created.Method.Should().Be(source.Method);
        created.Species.Should().Be(source.Species);
        batch.CatchProposals.Should().Equal(source, created);
        batch.CatchProposals.SelectMany(proposal => proposal.PhotoIds).Should().OnlyHaveUniqueItems();
        batch.TripProposals.Should().BeEmpty();
    }

    [Fact]
    public void ItShouldRejectASplitThatWouldEmptyTheSourceCatch()
    {
        // Arrange
        var batch = Batch();
        batch.AddPhoto(Photo());
        var source = Catch();
        batch.AddCatchProposal(source);
        Action split = () => batch.SplitCatch(CatchId, [PhotoId], SecondCatchId);

        // Act
        var assertion = split.Should();

        // Assert
        assertion.Throw<InvalidOperationException>();
        source.PhotoIds.Should().Equal(PhotoId);
        batch.CatchProposals.Should().ContainSingle();
    }

    [Fact]
    public void ItShouldRejectADuplicateSplitIdentityBeforeChangingMembership()
    {
        // Arrange
        var batch = Batch();
        batch.AddPhoto(Photo());
        batch.AddPhoto(Photo(SecondPhotoId, 1));
        var source = Catch(photoIds: [PhotoId, SecondPhotoId]);
        batch.AddCatchProposal(source);
        Action split = () => batch.SplitCatch(CatchId, [SecondPhotoId], CatchId);

        // Act
        var assertion = split.Should();

        // Assert
        assertion.Throw<InvalidOperationException>();
        source.PhotoIds.Should().Equal(PhotoId, SecondPhotoId);
    }

    [Fact]
    public void ItShouldMergeCatchesWithoutDuplicatingPhotosAndInvalidateTrips()
    {
        // Arrange
        var batch = Batch();
        batch.AddPhoto(Photo(PhotoId, 1));
        batch.AddPhoto(Photo(SecondPhotoId, 0));
        var primary = Catch(photoIds: [PhotoId]);
        var absorbed = Catch(SecondCatchId, [SecondPhotoId]);
        primary.MarkReviewed();
        absorbed.MarkReviewed();
        batch.AddCatchProposal(primary);
        batch.AddCatchProposal(absorbed);
        batch.AddTripProposal(Trip(catchIds: [CatchId, SecondCatchId]));

        // Act
        batch.MergeCatches(CatchId, SecondCatchId);

        // Assert
        primary.PhotoIds.Should().Equal(SecondPhotoId, PhotoId);
        primary.ReviewStatus.Should().Be(ImportCatchReviewStatusEnum.Draft);
        absorbed.IsRemoved.Should().BeTrue();
        batch.CatchProposals.Where(proposal => !proposal.IsRemoved).Should().ContainSingle();
        batch.TripProposals.Should().BeEmpty();
    }

    [Fact]
    public void ItShouldRequireCanonicalReviewWhenMergedValuesConflict()
    {
        // Arrange
        var batch = Batch();
        batch.AddPhoto(Photo());
        batch.AddPhoto(Photo(SecondPhotoId, 1));
        var primary = Catch();
        var absorbed = new ImportCatchProposalModel(
            SecondCatchId,
            [SecondPhotoId],
            ImportTimestampModel.UserConfirmed(CapturedOn.AddHours(1)),
            new ImportCatalogueSelectionModel(Guid.NewGuid(), "Bait", "Bait"),
            Species());
        batch.AddCatchProposal(primary);
        batch.AddCatchProposal(absorbed);

        // Act
        batch.MergeCatches(CatchId, SecondCatchId);
        Action reviewBeforeConfirmation = () => batch.MarkCatchReviewed(CatchId);

        // Assert
        reviewBeforeConfirmation.Should().Throw<InvalidOperationException>();

        // Act
        primary.ConfirmCanonicalValues();
        batch.MarkCatchReviewed(CatchId);

        // Assert
        primary.IsReadyForConfirmation.Should().BeTrue();
    }

    [Fact]
    public void ItShouldKeepCatchOverridesSeparateFromBatchDefaultsAndOtherCatches()
    {
        // Arrange
        var batch = Batch();
        batch.AddPhoto(Photo());
        batch.AddPhoto(Photo(SecondPhotoId, 1));
        var first = Catch();
        var second = Catch(SecondCatchId, [SecondPhotoId]);
        batch.AddCatchProposal(first);
        batch.AddCatchProposal(second);
        var method = new ImportCatalogueSelectionModel(Guid.NewGuid(), "Bait", "Bait");

        // Act
        batch.SetCatchMethod(CatchId, method);

        // Assert
        first.Method.Should().Be(method);
        second.Method.Should().Be(Method());
        batch.FishingMethod.Should().Be(Method());
    }

    [Fact]
    public void ItShouldRepresentEveryFirstMilestoneStage()
    {
        // Arrange
        var batch = Batch();
        var stages = Enum.GetValues<ImportStageEnum>();

        // Act
        foreach (var stage in stages)
        {
            batch.SetStage(stage);
        }

        // Assert
        stages.Should().Equal(
            ImportStageEnum.BatchDetails,
            ImportStageEnum.ChoosePhotos,
            ImportStageEnum.ReviewCatches,
            ImportStageEnum.CorrectCatches,
            ImportStageEnum.ReviewTrips,
            ImportStageEnum.Confirm);
        batch.Stage.Should().Be(ImportStageEnum.Confirm);
    }

    [Fact]
    public void ItShouldRepresentPhotoProcessingAndCancellationState()
    {
        // Arrange
        var batch = Batch();
        batch.BeginPhotoProcessing();

        // Act
        batch.Cancel();

        // Assert
        batch.IsCancelled.Should().BeTrue();
        batch.IsProcessingPhotos.Should().BeFalse();
        batch.CanProcessPhotos.Should().BeFalse();
    }

    private static ImportSelectedPhotoModel ReadyPersistencePhoto(Guid? id = null, int index = 0)
    {
        var photo = Photo(id ?? PhotoId, index);
        photo.SetPreparation(
            ImportPhotoPreparationStatusEnum.Ready,
            $"token-{index}",
            $"blob:thumbnail-{index}");
        return photo;
    }
}
