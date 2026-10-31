using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Photographs.Enums;
using FishingLogBook.Mobile.Features.Photographs.Models;
using FishingLogBook.Mobile.Features.Photographs.Services;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Photographs.ViewModels.PhotographCollectionEditorViewModelTests;

public sealed class WhenTestingTakePhoto : BasePhotographCollectionEditorViewModelTest
{
    [Fact]
    public async Task ItShouldNotCallTheEditorWhenTheUserIdIsMissing()
    {
        // Arrange
        Sut.UserId = Guid.Empty;

        // Act
        await Sut.TakePhotoAsync(CancellationToken.None);

        // Assert
        await MockEditor.DidNotReceive().TakePhotoAsync(
            Arg.Any<Guid>(),
            Arg.Any<IReadOnlyList<PhotographEditorItemModel>>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
        Sut.HasItems.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldApplyPreparedPhotosAndShowBusyWhileTheEditorRuns()
    {
        // Arrange
        var existing = Photo(0);
        var added = Photo(1);
        Sut.SetItems([existing]);
        var busy = false;
        MockEditor.TakePhotoAsync(
                OwnerUserId,
                Arg.Any<IReadOnlyList<PhotographEditorItemModel>>(),
                10,
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                busy = Sut.IsBusy;
                return PhotographEditorActionResultModel.Updated([existing, added]);
            });

        // Act
        await Sut.TakePhotoAsync(CancellationToken.None);

        // Assert
        busy.Should().BeTrue();
        Sut.IsBusy.Should().BeFalse();
        Sut.Items.Select(item => item.Id).Should().Equal(existing.Id, added.Id);
        await MockEditor.Received(1).TakePhotoAsync(
            OwnerUserId,
            Arg.Is<IReadOnlyList<PhotographEditorItemModel>>(items =>
                items.Count == 1 && items[0].Id == existing.Id),
            10,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldIgnoreASecondTakeWhileTheFirstIsInFlight()
    {
        // Arrange
        var pending = new TaskCompletionSource<PhotographEditorActionResultModel>();
        MockEditor.TakePhotoAsync(
                OwnerUserId,
                Arg.Any<IReadOnlyList<PhotographEditorItemModel>>(),
                10,
                Arg.Any<CancellationToken>())
            .Returns(_ => pending.Task);

        // Act
        var first = Sut.TakePhotoAsync(CancellationToken.None);
        await Sut.TakePhotoAsync(CancellationToken.None);
        pending.SetResult(PhotographEditorActionResultModel.Updated([Photo()]));
        await first;

        // Assert
        await MockEditor.Received(1).TakePhotoAsync(
            OwnerUserId,
            Arg.Any<IReadOnlyList<PhotographEditorItemModel>>(),
            10,
            Arg.Any<CancellationToken>());
        Sut.IsBusy.Should().BeFalse();
        Sut.Items.Should().ContainSingle();
    }

    [Theory]
    [InlineData(PhotographPreparationOutcomeEnum.PermissionDenied, "PhotoPermissionDenied")]
    [InlineData(PhotographPreparationOutcomeEnum.UnsupportedContentType, "PhotoUnsupportedFormat")]
    [InlineData(PhotographPreparationOutcomeEnum.TooLarge, "PhotoTooLarge")]
    [InlineData(PhotographPreparationOutcomeEnum.CouldNotPrepare, "PhotoPrepareFailed")]
    public async Task ItShouldSurfaceEditorFailureLabels(
        PhotographPreparationOutcomeEnum outcome,
        string key)
    {
        // Arrange
        MockEditor.TakePhotoAsync(
                OwnerUserId,
                Arg.Any<IReadOnlyList<PhotographEditorItemModel>>(),
                10,
                Arg.Any<CancellationToken>())
            .Returns(PhotographEditorActionResultModel.Unchanged([], outcome));

        // Act
        await Sut.TakePhotoAsync(CancellationToken.None);

        // Assert
        Sut.HasStatus.Should().BeTrue();
        Sut.StatusText.Should().Be(Text[key]);
        Sut.HasItems.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldKeepFrenchFailureLabelsWhenCultureChanges()
    {
        // Arrange
        MockEditor.TakePhotoAsync(
                OwnerUserId,
                Arg.Any<IReadOnlyList<PhotographEditorItemModel>>(),
                10,
                Arg.Any<CancellationToken>())
            .Returns(PhotographEditorActionResultModel.Unchanged(
                [],
                PhotographPreparationOutcomeEnum.PermissionDenied));
        await Sut.TakePhotoAsync(CancellationToken.None);

        // Act
        Text.SetCulture("fr-FR");

        // Assert
        Sut.StatusText.Should().Be(Text["PhotoPermissionDenied"]);
    }

    [Fact]
    public async Task ItShouldTreatACancelledTakeAsNoStatus()
    {
        // Arrange
        MockEditor.TakePhotoAsync(
                OwnerUserId,
                Arg.Any<IReadOnlyList<PhotographEditorItemModel>>(),
                10,
                Arg.Any<CancellationToken>())
            .Returns(PhotographEditorActionResultModel.Unchanged(
                [],
                PhotographPreparationOutcomeEnum.Cancelled));

        // Act
        await Sut.TakePhotoAsync(CancellationToken.None);

        // Assert
        Sut.HasStatus.Should().BeFalse();
        Sut.StatusText.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldIgnoreANullEditorResult()
    {
        // Arrange
        MockEditor.TakePhotoAsync(
                OwnerUserId,
                Arg.Any<IReadOnlyList<PhotographEditorItemModel>>(),
                10,
                Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<PhotographEditorActionResultModel>(null!));

        // Act
        var act = () => Sut.TakePhotoAsync(CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        Sut.HasItems.Should().BeFalse();
        Sut.IsBusy.Should().BeFalse();
    }
}
