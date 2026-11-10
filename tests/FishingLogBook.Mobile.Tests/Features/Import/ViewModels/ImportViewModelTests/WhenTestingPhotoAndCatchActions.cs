using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Import.Models;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Import.ViewModels.ImportViewModelTests;

public sealed class WhenTestingPhotoAndCatchActions : BaseImportCatchActionsTest
{
    [Fact]
    public async Task ItShouldIgnoreRemovingAPhotoThatIsNotInTheBatch()
    {
        // Arrange
        await ReviewPreparedPhotosAsync(PhotoAt(FirstPhotoId, 0, 0), PhotoAt(SecondPhotoId, 1, 2));

        // Act
        await Sut.RemovePhotoAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        Sut.Catches.Single().Photos.Should().HaveCount(2);
        await MockPreparation.DidNotReceive().RemoveAsync(Arg.Any<ImportSelectedPhotoModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReleaseThePreparedPhotoAndDropItFromItsCatchWhenRemoved()
    {
        // Arrange
        await ReviewPreparedPhotosAsync(PhotoAt(FirstPhotoId, 0, 0), PhotoAt(SecondPhotoId, 1, 2));

        // Act
        await Sut.RemovePhotoAsync(SecondPhotoId, CancellationToken.None);

        // Assert
        Sut.Catches.Single().Photos.Select(photo => photo.Photo.Id).Should().Equal(FirstPhotoId);
        await MockPreparation.Received(1).RemoveAsync(
            Arg.Is<ImportSelectedPhotoModel>(photo => photo.Id == SecondPhotoId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldToggleAPhotoSelectionOnAndOff()
    {
        // Arrange
        await ReviewPreparedPhotosAsync(PhotoAt(FirstPhotoId, 0, 0), PhotoAt(SecondPhotoId, 1, 2));
        var catchId = Sut.Catches.Single().Proposal.Id;

        // Act
        Sut.TogglePhotoSelection(catchId, SecondPhotoId);
        var selected = Sut.Catches.Single();
        Sut.TogglePhotoSelection(catchId, SecondPhotoId);
        var cleared = Sut.Catches.Single();

        // Assert
        selected.Photos.Single(photo => photo.Photo.Id == SecondPhotoId).IsSelected.Should().BeTrue();
        selected.CanSplit.Should().BeTrue();
        selected.CanRemoveSelected.Should().BeTrue();
        cleared.Photos.Should().OnlyContain(photo => !photo.IsSelected);
        cleared.CanSplit.Should().BeFalse();
        cleared.CanRemoveSelected.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldNotSplitWhenNoPhotoIsSelected()
    {
        // Arrange
        await ReviewPreparedPhotosAsync(PhotoAt(FirstPhotoId, 0, 0), PhotoAt(SecondPhotoId, 1, 2));
        var catchId = Sut.Catches.Single().Proposal.Id;

        // Act
        Sut.SplitSelectedPhotos(catchId);

        // Assert
        Sut.Catches.Should().ContainSingle();
    }

    [Fact]
    public async Task ItShouldMoveTheSelectedPhotosIntoANewCatchWhenSplitting()
    {
        // Arrange
        await ReviewPreparedPhotosAsync(PhotoAt(FirstPhotoId, 0, 0), PhotoAt(SecondPhotoId, 1, 2));
        var catchId = Sut.Catches.Single().Proposal.Id;
        Sut.TogglePhotoSelection(catchId, SecondPhotoId);

        // Act
        Sut.SplitSelectedPhotos(catchId);

        // Assert
        Sut.Catches.Should().HaveCount(2);
        Sut.Catches[0].Photos.Select(photo => photo.Photo.Id).Should().Equal(FirstPhotoId);
        Sut.Catches[1].Photos.Select(photo => photo.Photo.Id).Should().Equal(SecondPhotoId);
        Sut.Catches.SelectMany(row => row.Photos).Should().OnlyContain(photo => !photo.IsSelected);
    }

    [Fact]
    public async Task ItShouldNotRemoveAnythingWhenNoPhotoIsSelected()
    {
        // Arrange
        await ReviewPreparedPhotosAsync(PhotoAt(FirstPhotoId, 0, 0), PhotoAt(SecondPhotoId, 1, 2));
        var catchId = Sut.Catches.Single().Proposal.Id;

        // Act
        await Sut.RemoveSelectedPhotosAsync(catchId, CancellationToken.None);

        // Assert
        Sut.Catches.Single().Photos.Should().HaveCount(2);
        await MockPreparation.DidNotReceive().RemoveAsync(Arg.Any<ImportSelectedPhotoModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReleaseAndDropEverySelectedPhotoWhenRemovingTheSelection()
    {
        // Arrange
        await ReviewPreparedPhotosAsync(PhotoAt(FirstPhotoId, 0, 0), PhotoAt(SecondPhotoId, 1, 2), PhotoAt(ThirdPhotoId, 2, 3));
        var catchId = Sut.Catches.Single().Proposal.Id;
        Sut.TogglePhotoSelection(catchId, FirstPhotoId);
        Sut.TogglePhotoSelection(catchId, ThirdPhotoId);

        // Act
        await Sut.RemoveSelectedPhotosAsync(catchId, CancellationToken.None);

        // Assert
        Sut.Catches.Single().Photos.Select(photo => photo.Photo.Id).Should().Equal(SecondPhotoId);
        await MockPreparation.Received(1).RemoveAsync(
            Arg.Is<ImportSelectedPhotoModel>(photo => photo.Id == FirstPhotoId),
            Arg.Any<CancellationToken>());
        await MockPreparation.Received(1).RemoveAsync(
            Arg.Is<ImportSelectedPhotoModel>(photo => photo.Id == ThirdPhotoId),
            Arg.Any<CancellationToken>());
        await MockPreparation.DidNotReceive().RemoveAsync(
            Arg.Is<ImportSelectedPhotoModel>(photo => photo.Id == SecondPhotoId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepEveryPhotoOnceWhenTwoCatchesAreMerged()
    {
        // Arrange
        await ReviewPreparedPhotosAsync(PhotoAt(FirstPhotoId, 0, 0), PhotoAt(SecondPhotoId, 1, 20));
        var primary = Sut.Catches[0].Proposal.Id;
        var absorbed = Sut.Catches[1].Proposal.Id;

        // Act
        Sut.MergeCatches(primary, absorbed);

        // Assert
        var merged = Sut.Catches.Should().ContainSingle().Subject;
        merged.Proposal.Id.Should().Be(primary);
        merged.Photos.Select(photo => photo.Photo.Id).Should().Equal(FirstPhotoId, SecondPhotoId);
        merged.NeedsCanonicalReview.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldRefuseToMergeACatchWithItself()
    {
        // Arrange
        await ReviewPreparedPhotosAsync(PhotoAt(FirstPhotoId, 0, 0), PhotoAt(SecondPhotoId, 1, 20));
        var primary = Sut.Catches[0].Proposal.Id;

        // Act
        var act = () => Sut.MergeCatches(primary, primary);

        // Assert
        act.Should().Throw<InvalidOperationException>();
        Sut.Catches.Should().HaveCount(2);
    }

    [Fact]
    public async Task ItShouldReleaseEveryPhotoOfARemovedCatchAndKeepTheOthers()
    {
        // Arrange
        await ReviewPreparedPhotosAsync(PhotoAt(FirstPhotoId, 0, 0), PhotoAt(SecondPhotoId, 1, 20));
        var removed = Sut.Catches[0].Proposal.Id;

        // Act
        await Sut.RemoveCatchAsync(removed, CancellationToken.None);

        // Assert
        Sut.Catches.Should().ContainSingle().Which.Photos.Single().Photo.Id.Should().Be(SecondPhotoId);
        await MockPreparation.Received(1).RemoveAsync(
            Arg.Is<ImportSelectedPhotoModel>(photo => photo.Id == FirstPhotoId),
            Arg.Any<CancellationToken>());
        await MockPreparation.DidNotReceive().RemoveAsync(
            Arg.Is<ImportSelectedPhotoModel>(photo => photo.Id == SecondPhotoId),
            Arg.Any<CancellationToken>());
    }
}
