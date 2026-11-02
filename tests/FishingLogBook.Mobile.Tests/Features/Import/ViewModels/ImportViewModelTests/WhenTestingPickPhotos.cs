using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Import.Enums;
using FishingLogBook.Mobile.Features.Photographs.Enums;
using FishingLogBook.Mobile.Features.Photographs.Models;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Mobile.Tests.Features.Import.ViewModels.ImportViewModelTests;

public sealed class WhenTestingPickPhotos : BaseImportViewModelTest
{
    [Fact]
    public async Task ItShouldRestoreTheWizardAfterAnUnexpectedPreparationFailure()
    {
        // Arrange
        await Sut.LoadAsync(CancellationToken.None);
        await Sut.ContinueAsync(CancellationToken.None);
        MockMedia.PickPhotosAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(PhotographPickResultModel.FromFiles(
            [
                new PhotographPickedFileModel { FullPath = "photo.jpg", ContentType = "image/jpeg" }
            ]));
        MockPreparation.PrepareSelectionAsync(
                Arg.Any<IReadOnlyList<PhotographPickedFileModel>>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidDataException("corrupt photograph"));

        // Act
        await Sut.PickPhotosAsync(CancellationToken.None);

        // Assert
        Sut.HasMessage.Should().BeTrue();
        Sut.Message.Should().Be(Text["Import_PhotoFailed"]);
        Sut.IsBusy.Should().BeFalse();
        Sut.IsPhotosStep.Should().BeTrue();
        await Sut.PickPhotosAsync(CancellationToken.None);
        await MockPreparation.Received(2).PrepareSelectionAsync(
            Arg.Any<IReadOnlyList<PhotographPickedFileModel>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldClearProcessingStateWhenPreparationIsCancelled()
    {
        // Arrange
        await Sut.LoadAsync(CancellationToken.None);
        await Sut.ContinueAsync(CancellationToken.None);
        MockMedia.PickPhotosAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(PhotographPickResultModel.FromFiles(
            [
                new PhotographPickedFileModel { FullPath = "photo.jpg", ContentType = "image/jpeg" }
            ]));
        var started = new TaskCompletionSource();
        MockPreparation.PrepareSelectionAsync(
                Arg.Any<IReadOnlyList<PhotographPickedFileModel>>(),
                Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                started.SetResult();
                await Task.Delay(Timeout.Infinite, call.Arg<CancellationToken>());
                return Array.Empty<FishingLogBook.Mobile.Features.Import.Models.ImportSelectedPhotoModel>();
            });

        // Act
        var pick = Sut.PickPhotosAsync(CancellationToken.None);
        await started.Task;
        await Sut.LeaveAsync();
        await pick;

        // Assert
        Sut.IsBusy.Should().BeFalse();
        Sut.HasMessage.Should().BeFalse();
        await MockPreparation.Received().ClearAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldLeaveTheWizardUsableWhenThePickerIsDenied()
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
        Sut.IsPhotosStep.Should().BeTrue();
        Sut.IsBusy.Should().BeFalse();
        await MockPreparation.DidNotReceive().PrepareSelectionAsync(
            Arg.Any<IReadOnlyList<PhotographPickedFileModel>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepADuplicateUntilTheAnglerChoosesToKeepBoth()
    {
        // Arrange
        var photo = ReadyPhoto();
        photo.SetDuplicateState(
            ImportDuplicateStatusEnum.Duplicate,
            ImportDuplicateReasonEnum.IdenticalPreparedBytes,
            [Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")]);
        await Sut.LoadAsync(CancellationToken.None);
        await Sut.ContinueAsync(CancellationToken.None);
        MockMedia.PickPhotosAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(PhotographPickResultModel.FromFiles(
            [
                new PhotographPickedFileModel { FullPath = "photo.jpg", ContentType = "image/jpeg" }
            ]));
        MockPreparation.PrepareSelectionAsync(
                Arg.Any<IReadOnlyList<PhotographPickedFileModel>>(),
                Arg.Any<CancellationToken>())
            .Returns([photo]);
        await Sut.PickPhotosAsync(CancellationToken.None);

        // Act
        Sut.KeepDuplicate(photo.Id);

        // Assert
        Sut.Photos.Single().Photo.DuplicateDecision.Should().Be(ImportDuplicateDecisionEnum.KeepBoth);
        Sut.Photos.Single().Photo.RequiresDuplicateDecision.Should().BeFalse();
        Sut.CanContinue.Should().BeTrue();
    }
}
