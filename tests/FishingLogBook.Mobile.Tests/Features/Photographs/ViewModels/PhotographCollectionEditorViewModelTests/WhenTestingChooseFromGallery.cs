using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Photographs.Enums;
using FishingLogBook.Mobile.Features.Photographs.Models;
using FishingLogBook.Mobile.Features.Photographs.Services;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Photographs.ViewModels.PhotographCollectionEditorViewModelTests;

public sealed class WhenTestingChooseFromGallery : BasePhotographCollectionEditorViewModelTest
{
    [Fact]
    public async Task ItShouldNotCallTheEditorWhenTheUserIdIsMissing()
    {
        // Arrange
        Sut.UserId = Guid.Empty;

        // Act
        await Sut.ChooseFromGalleryAsync(CancellationToken.None);

        // Assert
        await MockEditor.DidNotReceive().ChooseFromGalleryAsync(
            Arg.Any<Guid>(),
            Arg.Any<IReadOnlyList<PhotographEditorItemModel>>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldApplyGalleryPhotos()
    {
        // Arrange
        var added = Photo();
        MockEditor.ChooseFromGalleryAsync(
                OwnerUserId,
                Arg.Any<IReadOnlyList<PhotographEditorItemModel>>(),
                10,
                Arg.Any<CancellationToken>())
            .Returns(PhotographEditorActionResultModel.Updated([added]));

        // Act
        await Sut.ChooseFromGalleryAsync(CancellationToken.None);

        // Assert
        Sut.Items.Should().ContainSingle(item => item.Id == added.Id);
        await MockEditor.Received(1).ChooseFromGalleryAsync(
            OwnerUserId,
            Arg.Is<IReadOnlyList<PhotographEditorItemModel>>(items => items.Count == 0),
            10,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSurfaceAGalleryPermissionFailure()
    {
        // Arrange
        MockEditor.ChooseFromGalleryAsync(
                OwnerUserId,
                Arg.Any<IReadOnlyList<PhotographEditorItemModel>>(),
                10,
                Arg.Any<CancellationToken>())
            .Returns(PhotographEditorActionResultModel.Unchanged(
                [],
                PhotographPreparationOutcomeEnum.PermissionDenied));

        // Act
        await Sut.ChooseFromGalleryAsync(CancellationToken.None);

        // Assert
        Sut.StatusText.Should().Be(Text["PhotoPermissionDenied"]);
        Sut.HasItems.Should().BeFalse();
    }
}
