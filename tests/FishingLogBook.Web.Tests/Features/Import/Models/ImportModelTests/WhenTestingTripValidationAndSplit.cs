using AwesomeAssertions;
using FishingLogBook.Web.Features.Import.Enums;
using FishingLogBook.Web.Features.Import.Models;

namespace FishingLogBook.Web.Tests.Features.Import.Models.ImportModelTests;

public class WhenTestingTripValidationAndSplit : BaseImportModelTest
{
    private static ImportSelectedPhotoModel ReadyPhoto(Guid id, int index, ImportLocationModel? location = null)
    {
        var photo = Photo(id, index);
        photo.SetPreparation(ImportPhotoPreparationStatusEnum.Ready, $"token-{index}", $"blob:thumbnail-{index}");
        if (location is not null)
        {
            photo.SetLocation(location);
        }

        return photo;
    }

    private static ImportBatchModel BatchWithTwoPhotos(ImportLocationModel? first = null, ImportLocationModel? second = null)
    {
        var batch = Batch();
        batch.AddPhoto(ReadyPhoto(PhotoId, 0, first));
        batch.AddPhoto(ReadyPhoto(SecondPhotoId, 1, second));
        batch.AddCatchProposal(Catch(photoIds: [PhotoId, SecondPhotoId]));
        return batch;
    }

    [Fact]
    public void ItShouldRejectAnActiveTripWhoseDecisionHasNotBeenMade()
    {
        // Arrange
        var batch = Batch();
        var catchProposal = Catch();
        catchProposal.MarkReviewed();
        batch.AddPhoto(ReadyPhoto(PhotoId, 0));
        batch.AddCatchProposal(catchProposal);
        batch.AddTripProposal(Trip());

        // Act
        var act = () => batch.ValidateForPersistence(CapturedOn.AddDays(1));

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*valid final decision*");
        batch.IsReadyForConfirmation.Should().BeFalse();
    }

    [Fact]
    public void ItShouldAcceptAnActiveTripOnceItsDecisionHasBeenMade()
    {
        // Arrange
        var batch = Batch();
        var catchProposal = Catch();
        catchProposal.MarkReviewed();
        batch.AddPhoto(ReadyPhoto(PhotoId, 0));
        batch.AddCatchProposal(catchProposal);
        var trip = Trip();
        batch.AddTripProposal(trip);
        batch.DecideTrip(trip.Id, ImportTripDecisionEnum.CreateNew);

        // Act
        var act = () => batch.ValidateForPersistence(CapturedOn.AddDays(1));

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ItShouldRejectASplitWithoutANewCatchIdentity()
    {
        // Arrange
        var batch = BatchWithTwoPhotos();

        // Act
        var act = () => batch.SplitCatch(CatchId, [SecondPhotoId], Guid.Empty);

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*new unique Catch proposal identity*");
        batch.CatchProposals.Should().ContainSingle();
    }

    [Fact]
    public void ItShouldRejectASplitThatSelectsNoPhotographs()
    {
        // Arrange
        var batch = BatchWithTwoPhotos();

        // Act
        var act = () => batch.SplitCatch(CatchId, [], SecondCatchId);

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*some, but not all*");
        batch.CatchProposals.Single().PhotoIds.Should().Equal(PhotoId, SecondPhotoId);
    }

    [Fact]
    public void ItShouldRejectASplitThatSelectsAPhotographFromAnotherCatch()
    {
        // Arrange
        var batch = BatchWithTwoPhotos();

        // Act
        var act = () => batch.SplitCatch(CatchId, [Guid.NewGuid()], SecondCatchId);

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*some, but not all*");
        batch.CatchProposals.Single().PhotoIds.Should().Equal(PhotoId, SecondPhotoId);
    }

    [Fact]
    public void ItShouldFlagConflictingGpsWhenASplitMovesPhotographsFromDifferentPlaces()
    {
        // Arrange
        var batch = Batch();
        batch.AddPhoto(ReadyPhoto(PhotoId, 0));
        batch.AddPhoto(ReadyPhoto(SecondPhotoId, 1, new ImportLocationModel(53.1, -9.1, true)));
        var thirdPhotoId = Guid.NewGuid();
        batch.AddPhoto(ReadyPhoto(thirdPhotoId, 2, new ImportLocationModel(54.2, -8.2, true)));
        batch.AddCatchProposal(Catch(photoIds: [PhotoId, SecondPhotoId, thirdPhotoId]));

        // Act
        var created = batch.SplitCatch(CatchId, [SecondPhotoId, thirdPhotoId], SecondCatchId);

        // Assert
        created.Reasons.Should().Contain(ImportCatchProposalReasonEnum.ConflictingGps);
        created.Location.Should().BeNull();
    }

    [Fact]
    public void ItShouldInheritTheLocationWhenASplitMovesPhotographsFromOnePlace()
    {
        // Arrange
        var batch = Batch();
        batch.AddPhoto(ReadyPhoto(PhotoId, 0));
        batch.AddPhoto(ReadyPhoto(SecondPhotoId, 1, new ImportLocationModel(53.1, -9.1, true)));
        batch.AddCatchProposal(Catch(photoIds: [PhotoId, SecondPhotoId]));

        // Act
        var created = batch.SplitCatch(CatchId, [SecondPhotoId], SecondCatchId);

        // Assert
        created.Reasons.Should().NotContain(ImportCatchProposalReasonEnum.ConflictingGps);
        created.Location!.Latitude.Should().Be(53.1);
        created.Location.Longitude.Should().Be(-9.1);
    }
}
