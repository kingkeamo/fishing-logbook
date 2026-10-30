using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Photographs.Enums;
using FishingLogBook.Mobile.Features.Photographs.Models;
using FishingLogBook.Mobile.Features.Photographs.Services;
using FishingLogBook.Mobile.IO.Adapters;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Photographs.Models;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Photographs.Services.PhotographCollectionEditorServiceTests;

public class WhenTestingEditorActions
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    [Fact]
    public async Task ShouldReturnPermissionDeniedWhenCameraDenied()
    {
        var media = Substitute.For<IPhotographMediaAdapter>();
        media.CapturePhotoAsync(Arg.Any<CancellationToken>())
            .Returns(PhotographPickResultModel.PermissionDenied);
        var sut = new PhotographCollectionEditorService(media, Substitute.For<IPhotographPreparationService>());

        var result = await sut.TakePhotoAsync(UserId, Array.Empty<PhotographEditorItemModel>(), 10, CancellationToken.None);

        result.Outcome.Should().Be(PhotographPreparationOutcomeEnum.PermissionDenied);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ShouldReturnCancelledWhenGalleryCancelled()
    {
        var media = Substitute.For<IPhotographMediaAdapter>();
        media.PickPhotosAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(PhotographPickResultModel.Cancelled);
        var sut = new PhotographCollectionEditorService(media, Substitute.For<IPhotographPreparationService>());

        var result = await sut.ChooseFromGalleryAsync(UserId, Array.Empty<PhotographEditorItemModel>(), 10, CancellationToken.None);

        result.Outcome.Should().Be(PhotographPreparationOutcomeEnum.Cancelled);
    }

    [Fact]
    public void ShouldReorderAndRemoveItems()
    {
        var media = Substitute.For<IPhotographMediaAdapter>();
        var sut = new PhotographCollectionEditorService(media, Substitute.For<IPhotographPreparationService>());
        var first = CreateItem(0);
        var second = CreateItem(1);
        var third = CreateItem(2);
        var items = new[] { first, second, third };

        var moved = sut.MoveEarlier(items, second.Id);
        moved.Items[0].Id.Should().Be(second.Id);
        moved.Items[1].Id.Should().Be(first.Id);

        var removed = sut.Remove(moved.Items, second.Id);
        removed.Items.Should().HaveCount(2);
        removed.Items.Select(item => item.SortOrder).Should().Equal(0, 1);
    }

    [Fact]
    public async Task ShouldAppendPreparedGalleryPhotos()
    {
        var sourcePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.jpg");
        await File.WriteAllBytesAsync(sourcePath, [0xFF, 0xD8, 0xFF, 0xD9]);
        try
        {
            var media = Substitute.For<IPhotographMediaAdapter>();
            media.PickPhotosAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(PhotographPickResultModel.FromFiles(
                [
                    new PhotographPickedFileModel
                    {
                        FullPath = sourcePath,
                        ContentType = PhotographContentTypeConstants.Jpeg,
                        LastModified = DateTimeOffset.UtcNow
                    }
                ]));
            var preparation = Substitute.For<IPhotographPreparationService>();
            var photoId = Guid.NewGuid();
            preparation.PrepareFromFileAsync(
                    Arg.Any<Guid>(),
                    Arg.Any<string>(),
                    Arg.Any<string>(),
                    Arg.Any<DateTimeOffset?>(),
                    PhotographSourceEnum.Gallery,
                    Arg.Any<CancellationToken>())
                .Returns(PhotographPreparationResultModel.Prepared(
                    new PhotographPreparedFileModel
                    {
                        PhotographId = photoId,
                        RelativeFilePath = $"{UserId:N}/{photoId:N}.jpg",
                        ContentType = PhotographContentTypeConstants.Jpeg,
                        AbsolutePath = sourcePath
                    },
                    PhotographMetadataModel.Empty));
            var sut = new PhotographCollectionEditorService(media, preparation);

            var result = await sut.ChooseFromGalleryAsync(UserId, Array.Empty<PhotographEditorItemModel>(), 10, CancellationToken.None);

            result.Outcome.Should().Be(PhotographPreparationOutcomeEnum.Prepared);
            result.Items.Should().ContainSingle(item => item.Id == photoId);
        }
        finally
        {
            if (File.Exists(sourcePath))
            {
                File.Delete(sourcePath);
            }
        }
    }

    private static PhotographEditorItemModel CreateItem(int sortOrder)
    {
        var id = Guid.NewGuid();
        return new PhotographEditorItemModel
        {
            Id = id,
            SortOrder = sortOrder,
            PreviewPath = $"/tmp/{id:N}.jpg",
            RelativeFilePath = $"{UserId:N}/{id:N}.jpg",
            ContentType = PhotographContentTypeConstants.Jpeg
        };
    }
}
