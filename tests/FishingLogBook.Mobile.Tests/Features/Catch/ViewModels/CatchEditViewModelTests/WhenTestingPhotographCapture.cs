using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Catch.Offline.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Models;
using FishingLogBook.Mobile.Features.Photographs.Enums;
using FishingLogBook.Mobile.Features.Photographs.Models;
using FishingLogBook.Mobile.Features.Photographs.Services;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Catch.ViewModels.CatchEditViewModelTests;

public class WhenTestingPhotographCapture : BaseCatchEditViewModelTest
{
    private static PhotographEditorItemModel Photo()
    {
        return new PhotographEditorItemModel
        {
            Id = Guid.NewGuid(),
            SortOrder = 0,
            PreviewPath = "photo.jpg",
            RelativeFilePath = "photo.jpg",
            ContentType = "image/jpeg"
        };
    }

    private void ReturnCapture(PhotographEditorActionResultModel result)
    {
        MockPhotographEditor.TakePhotoAsync(
                OwnerUserId,
                Arg.Any<IReadOnlyList<PhotographEditorItemModel>>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(result);
    }

    [Fact]
    public async Task ItShouldKeepSaveDisabledAndReportPrepareFailureWhenCapturePreparationThrows()
    {
        // Arrange
        await Sut.InitializeAsync(null, CancellationToken.None);
        MockPhotographEditor.TakePhotoAsync(
                OwnerUserId,
                Arg.Any<IReadOnlyList<PhotographEditorItemModel>>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns<Task<PhotographEditorActionResultModel>>(_ => throw new IOException("disk"));

        // Act
        var act = () => Sut.Photographs.TakePhotoAsync(CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        Sut.Photographs.StatusText.Should().Be(Text["PhotoPrepareFailed"]);
        Sut.Photographs.Items.Should().BeEmpty();
        Sut.CanSave.Should().BeFalse();
        Sut.ValidationKey.Should().Be("CatchSaveRequiresPhoto");
        Sut.Photographs.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldLeaveTheEditorUsableWhenCaptureIsCancelled()
    {
        // Arrange
        await Sut.InitializeAsync(null, CancellationToken.None);
        ReturnCapture(PhotographEditorActionResultModel.Unchanged([], PhotographPreparationOutcomeEnum.Cancelled));

        // Act
        await Sut.Photographs.TakePhotoAsync(CancellationToken.None);
        Sut.SelectMethod("Fly");

        // Assert
        Sut.Photographs.HasStatus.Should().BeFalse();
        Sut.Photographs.IsBusy.Should().BeFalse();
        Sut.CanSave.Should().BeFalse();
        Sut.IsMethodSelected("Fly").Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldNotThrowWhenTheEditorIsInteractedWithAfterDisposal()
    {
        // Arrange
        await Sut.InitializeAsync(null, CancellationToken.None);
        Sut.SelectMethod("Fly");
        Sut.Dispose();

        // Act
        var act = async () =>
        {
            Sut.SelectMethod("Lure");
            Sut.SelectSpecies("Pike");
            Sut.Notes = "after dispose";
            await Sut.OnDisappearingAsync(CancellationToken.None);
            await Sut.SaveAsync(CancellationToken.None);
            Sut.Photographs.SetItems([Photo()]);
        };

        // Assert
        await act.Should().NotThrowAsync();
        await MockCatchStore.DidNotReceive().SaveAsync(Arg.Any<LocalCatchSaveArgs>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSaveADraftWithTheCapturedPhotoWhenThePageDisappears()
    {
        // Arrange
        await Sut.InitializeAsync(null, CancellationToken.None);
        var photo = Photo();
        ReturnCapture(PhotographEditorActionResultModel.Updated([photo]));
        MockCatchStore.SaveAsync(Arg.Any<LocalCatchSaveArgs>(), Arg.Any<CancellationToken>())
            .Returns(call => LocalCatchSaveResultModel.Saved(call.Arg<LocalCatchSaveArgs>().Id, 1));
        await Sut.Photographs.TakePhotoAsync(CancellationToken.None);

        // Act
        await Sut.OnDisappearingAsync(CancellationToken.None);

        // Assert
        await MockCatchStore.Received(1).SaveAsync(
            Arg.Is<LocalCatchSaveArgs>(args =>
                args.Lifecycle == LocalCatchLifecycleEnum.Draft
                && args.Photographs.Count == 1
                && args.Photographs[0].Id == photo.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRegisterTheCapturedPhotoClearTheRequiredPhotoMessageAndEnableSave()
    {
        // Arrange
        await Sut.InitializeAsync(null, CancellationToken.None);
        Sut.ValidationText.Should().Be(Text["CatchSaveRequiresPhoto"]);
        Sut.CanSave.Should().BeFalse();
        var photo = Photo();
        ReturnCapture(PhotographEditorActionResultModel.Updated([photo]));
        var notifications = new List<string?>();
        Sut.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        // Act
        await Sut.Photographs.TakePhotoAsync(CancellationToken.None);

        // Assert
        Sut.Photographs.Items.Should().ContainSingle(item => item.Id == photo.Id);
        Sut.HasValidation.Should().BeFalse();
        Sut.ValidationText.Should().BeEmpty();
        Sut.CanSave.Should().BeTrue();
        Sut.ShowCaughtOn.Should().BeTrue();
        notifications.Should().Contain([nameof(Sut.CanSave), nameof(Sut.HasValidation), nameof(Sut.ValidationText), nameof(Sut.ShowCaughtOn)]);
        await MockPhotographEditor.Received(1).TakePhotoAsync(
            OwnerUserId,
            Arg.Is<IReadOnlyList<PhotographEditorItemModel>>(items => items.Count == 0),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }
}
