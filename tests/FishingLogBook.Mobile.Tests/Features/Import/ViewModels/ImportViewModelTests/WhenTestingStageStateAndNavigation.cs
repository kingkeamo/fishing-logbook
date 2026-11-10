using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Import.Enums;
using FishingLogBook.Mobile.Features.Photographs.Models;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Import.ViewModels.ImportViewModelTests;

public sealed class WhenTestingStageStateAndNavigation : BaseImportCatchActionsTest
{
    [Fact]
    public async Task ItShouldDescribeTheBatchDetailsStep()
    {
        // Arrange
        // Act
        await Sut.LoadAsync(CancellationToken.None);

        // Assert
        Sut.Stage.Should().Be(ImportStageEnum.BatchDetails);
        Sut.StepNumber.Should().Be(1);
        Sut.StepCount.Should().Be(5);
        Sut.StepTitle.Should().Be(Text["Import_BatchDetailsTitle"]);
        Sut.ContinueText.Should().Be(Text["Import_ReviewCatches"]);
        Sut.IsBatchStep.Should().BeTrue();
        Sut.CanGoBack.Should().BeFalse();
        Sut.CanContinue.Should().BeTrue();
        Sut.ContinueHint.Should().BeEmpty();
        Sut.HasContinueHint.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldDescribeTheChoosePhotosStepAndHintUntilPhotosAreReady()
    {
        // Arrange
        await Sut.LoadAsync(CancellationToken.None);

        // Act
        await Sut.ContinueAsync(CancellationToken.None);

        // Assert
        Sut.Stage.Should().Be(ImportStageEnum.ChoosePhotos);
        Sut.StepNumber.Should().Be(2);
        Sut.StepTitle.Should().Be(Text["Import_ChoosePhotosTitle"]);
        Sut.IsPhotosStep.Should().BeTrue();
        Sut.CanGoBack.Should().BeTrue();
        Sut.CanContinue.Should().BeFalse();
        Sut.ContinueHint.Should().Be(Text["Import_ChoosePhotosHint"]);
        Sut.HasContinueHint.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldGoBackFromChoosePhotosToBatchDetails()
    {
        // Arrange
        await Sut.LoadAsync(CancellationToken.None);
        await Sut.ContinueAsync(CancellationToken.None);

        // Act
        Sut.Back();

        // Assert
        Sut.Stage.Should().Be(ImportStageEnum.BatchDetails);
    }

    [Fact]
    public async Task ItShouldDescribeTheCatchReviewStepAndGoBackToThePhotos()
    {
        // Arrange
        await ReviewPreparedPhotoAsync(ReadyPhoto());

        // Act
        var title = Sut.StepTitle;
        var continueText = Sut.ContinueText;
        var hint = Sut.ContinueHint;
        Sut.Back();

        // Assert
        title.Should().Be(Text["Import_ReviewTitle"]);
        continueText.Should().Be(Text["Import_ContinueToTrips"]);
        hint.Should().NotBeEmpty();
        Sut.Stage.Should().Be(ImportStageEnum.ChoosePhotos);
    }

    [Fact]
    public async Task ItShouldDescribeTheTripReviewStepAndGoBackToTheCatches()
    {
        // Arrange
        await ReachTripReviewAsync();

        // Act
        var title = Sut.StepTitle;
        var number = Sut.StepNumber;
        var hint = Sut.ContinueHint;
        var canContinueBeforeDeciding = Sut.CanContinue;
        Sut.DecideCreateTrip(Sut.Trips.Single().Proposal.Id);
        var canContinueAfterDeciding = Sut.CanContinue;
        Sut.Back();

        // Assert
        title.Should().Be(Text["Import_TripSuggestionsTitle"]);
        number.Should().Be(4);
        hint.Should().Be(Text["Import_ChooseTripHint"]);
        canContinueBeforeDeciding.Should().BeFalse();
        canContinueAfterDeciding.Should().BeTrue();
        Sut.Stage.Should().Be(ImportStageEnum.ReviewCatches);
    }

    [Fact]
    public async Task ItShouldDescribeTheConfirmStepBeforeSaving()
    {
        // Arrange
        // Act
        await ReachConfirmAsync();

        // Assert
        Sut.Stage.Should().Be(ImportStageEnum.Confirm);
        Sut.StepNumber.Should().Be(5);
        Sut.StepTitle.Should().Be(Text["Import_ConfirmTitle"]);
        Sut.ContinueText.Should().Be(Text["Import_ConfirmImport"]);
        Sut.StepDescription.Should().Be(Text["Import_NotSaved"]);
        Sut.IsConfirmStep.Should().BeTrue();
        Sut.IsSuccessStep.Should().BeFalse();
        Sut.CanContinue.Should().BeTrue();
        Sut.CanGoBack.Should().BeTrue();
        Sut.ContinueHint.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldGoBackFromConfirmToTheCatchReview()
    {
        // Arrange
        await ReachConfirmAsync();

        // Act
        Sut.Back();

        // Assert
        Sut.Stage.Should().Be(ImportStageEnum.ReviewCatches);
    }

    [Fact]
    public async Task ItShouldRefuseToGoBackFromTheFirstStep()
    {
        // Arrange
        await Sut.LoadAsync(CancellationToken.None);

        // Act
        Sut.Back();

        // Assert
        Sut.Stage.Should().Be(ImportStageEnum.BatchDetails);
    }

    [Fact]
    public async Task ItShouldReportAFailureWhenThePickerCannotOpen()
    {
        // Arrange
        await Sut.LoadAsync(CancellationToken.None);
        await Sut.ContinueAsync(CancellationToken.None);
        MockMedia.PickPhotosAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(PhotographPickResultModel.Unavailable);

        // Act
        await Sut.PickPhotosAsync(CancellationToken.None);

        // Assert
        Sut.Message.Should().Be(Text["Import_PhotoFailed"]);
        Sut.IsBusy.Should().BeFalse();
        await MockPreparation.DidNotReceive().PrepareSelectionAsync(
            Arg.Any<IReadOnlyList<PhotographPickedFileModel>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportAFailureWhenPhotoPermissionIsDenied()
    {
        // Arrange
        await Sut.LoadAsync(CancellationToken.None);
        await Sut.ContinueAsync(CancellationToken.None);
        MockMedia.PickPhotosAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(PhotographPickResultModel.PermissionDenied);

        // Act
        await Sut.PickPhotosAsync(CancellationToken.None);

        // Assert
        Sut.Message.Should().Be(Text["Import_PhotoFailed"]);
    }

    [Fact]
    public async Task ItShouldIgnoreACancelledPhotoPicker()
    {
        // Arrange
        await Sut.LoadAsync(CancellationToken.None);
        await Sut.ContinueAsync(CancellationToken.None);
        MockMedia.PickPhotosAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(PhotographPickResultModel.Cancelled);

        // Act
        await Sut.PickPhotosAsync(CancellationToken.None);

        // Assert
        Sut.HasMessage.Should().BeFalse();
        Sut.HasPhotos.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldFlagASelectionOverThePhotoLimitWithoutPreparingAnything()
    {
        // Arrange
        await Sut.LoadAsync(CancellationToken.None);
        await Sut.ContinueAsync(CancellationToken.None);
        var files = Enumerable.Range(0, 21)
            .Select(index => new PhotographPickedFileModel { FullPath = $"photo-{index}.jpg", ContentType = "image/jpeg" })
            .ToArray();
        MockMedia.PickPhotosAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(PhotographPickResultModel.FromFiles(files));

        // Act
        await Sut.PickPhotosAsync(CancellationToken.None);

        // Assert
        Sut.SelectionLimitExceeded.Should().BeTrue();
        await MockPreparation.DidNotReceive().PrepareSelectionAsync(
            Arg.Any<IReadOnlyList<PhotographPickedFileModel>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotPickPhotosWhileTheDefaultsAreInvalid()
    {
        // Arrange
        MockPreferences.RefreshAsync(Arg.Any<CancellationToken>())
            .Returns(FishingLogBook.Mobile.Api.Models.ApiResultModel<FishingLogBook.Mobile.Features.Profile.Models.AnglerPreferencesModel>
                .Succeeded(FishingLogBook.Mobile.Features.Profile.Models.AnglerPreferencesModel.Empty));
        await Sut.LoadAsync(CancellationToken.None);

        // Act
        await Sut.PickPhotosAsync(CancellationToken.None);

        // Assert
        await MockMedia.DidNotReceive().PickPhotosAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        Sut.CanContinue.Should().BeFalse();
    }
}
