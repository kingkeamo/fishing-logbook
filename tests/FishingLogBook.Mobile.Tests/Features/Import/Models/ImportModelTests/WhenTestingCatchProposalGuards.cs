using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Import.Enums;
using FishingLogBook.Mobile.Features.Import.Models;

namespace FishingLogBook.Mobile.Tests.Features.Import.Models.ImportModelTests;

public class WhenTestingCatchProposalGuards : BaseImportModelTest
{
    [Fact]
    public void ItShouldRequireAProposalIdentity()
    {
        // Arrange
        // Act
        var act = () => Catch(Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ItShouldRequireAtLeastOnePhoto()
    {
        // Arrange
        // Act
        var act = () => Catch(CatchId, []);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ItShouldRejectAnEmptyPhotoIdentity()
    {
        // Arrange
        // Act
        var act = () => Catch(CatchId, [PhotoId, Guid.Empty]);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ItShouldRejectDuplicatePhotosAtConstruction()
    {
        // Arrange
        // Act
        var act = () => Catch(CatchId, [PhotoId, PhotoId]);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ItShouldRejectAddingAnEmptyOrAlreadyPresentPhoto(bool empty)
    {
        // Arrange
        var proposal = Catch();

        // Act
        var act = () => proposal.AddPhoto(empty ? Guid.Empty : PhotoId);

        // Assert
        act.Should().Throw<InvalidOperationException>();
        proposal.PhotoIds.Should().Equal(PhotoId);
    }

    [Fact]
    public void ItShouldReopenReviewWhenAPhotoIsAdded()
    {
        // Arrange
        var proposal = Catch();
        proposal.MarkReviewed();

        // Act
        proposal.AddPhoto(SecondPhotoId);

        // Assert
        proposal.PhotoIds.Should().Equal(PhotoId, SecondPhotoId);
        proposal.ReviewStatus.Should().Be(ImportCatchReviewStatusEnum.Draft);
    }

    [Fact]
    public void ItShouldReplaceTheAssignedPhotosAndReopenReview()
    {
        // Arrange
        var proposal = Catch();
        proposal.MarkReviewed();

        // Act
        proposal.SetPhotos([SecondPhotoId, PhotoId]);

        // Assert
        proposal.PhotoIds.Should().Equal(SecondPhotoId, PhotoId);
        proposal.ReviewStatus.Should().Be(ImportCatchReviewStatusEnum.Draft);
    }

    [Fact]
    public void ItShouldRejectReplacingPhotosWithAnInvalidSet()
    {
        // Arrange
        var proposal = Catch();

        // Act
        var none = () => proposal.SetPhotos([]);
        var empty = () => proposal.SetPhotos([Guid.Empty]);
        var duplicate = () => proposal.SetPhotos([PhotoId, PhotoId]);

        // Assert
        none.Should().Throw<InvalidOperationException>();
        empty.Should().Throw<InvalidOperationException>();
        duplicate.Should().Throw<InvalidOperationException>();
        proposal.PhotoIds.Should().Equal(PhotoId);
    }

    [Fact]
    public void ItShouldRemainActiveWhileAPhotoRemainsAndBecomeRemovedWithTheLastOne()
    {
        // Arrange
        var proposal = Catch(CatchId, [PhotoId, SecondPhotoId]);

        // Act
        proposal.RemovePhoto(PhotoId);
        var stillActive = !proposal.IsRemoved;
        proposal.RemovePhoto(SecondPhotoId);

        // Assert
        stillActive.Should().BeTrue();
        proposal.IsRemoved.Should().BeTrue();
    }

    [Fact]
    public void ItShouldBeRemovableDirectly()
    {
        // Arrange
        var proposal = Catch();

        // Act
        proposal.Remove();

        // Assert
        proposal.IsRemoved.Should().BeTrue();
    }

    [Fact]
    public void ItShouldNeedCanonicalReviewAfterItIsRequiredAndClearItWhenConfirmed()
    {
        // Arrange
        var proposal = Catch();
        proposal.MarkReviewed();

        // Act
        proposal.RequireCanonicalReview();
        var required = proposal.RequiresCanonicalReview;
        proposal.ConfirmCanonicalValues();

        // Assert
        required.Should().BeTrue();
        proposal.RequiresCanonicalReview.Should().BeFalse();
        proposal.ReviewStatus.Should().Be(ImportCatchReviewStatusEnum.Draft);
    }

    [Fact]
    public void ItShouldRefuseToConfirmDisplayedValuesWhenThereAreNone()
    {
        // Arrange
        var proposal = Catch(caughtOn: ImportTimestampModel.Missing());

        // Act
        var act = proposal.ConfirmDisplayedValues;

        // Assert
        proposal.CanConfirmDisplayedValues.Should().BeFalse();
        act.Should().Throw<InvalidOperationException>();
    }
}
