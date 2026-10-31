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
    public async Task ItShouldPrepareAllValidSelectionsDespiteAnUnsupportedFileBetweenThem()
    {
        // Arrange
        var mockMedia = Substitute.For<IPhotographMediaAdapter>();
        var mockPreparation = Substitute.For<IPhotographPreparationService>();
        var paths = new[] { "first.jpg", "unsupported.heic", "last.jpg" };
        mockMedia.PickPhotosAsync(10, Arg.Any<CancellationToken>()).Returns(
            PhotographPickResultModel.FromFiles(paths.Select(path => new PhotographPickedFileModel
            {
                FullPath = path,
                ContentType = "image/jpeg"
            }).ToArray()));
        mockPreparation.PrepareFromFileAsync(UserId, Arg.Any<string>(), "image/jpeg", null,
                PhotographSourceEnum.Gallery, Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<string>(1) == "unsupported.heic"
                ? PhotographPreparationResultModel.Unsupported
                : PhotographPreparationResultModel.Prepared(new PhotographPreparedFileModel
                {
                    PhotographId = Guid.NewGuid(),
                    RelativeFilePath = call.ArgAt<string>(1),
                    AbsolutePath = call.ArgAt<string>(1),
                    ContentType = "image/jpeg"
                }, PhotographMetadataModel.Empty));
        var sut = new PhotographCollectionEditorService(mockMedia, mockPreparation);

        // Act
        var result = await sut.ChooseFromGalleryAsync(UserId, [], 10, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(PhotographPreparationOutcomeEnum.UnsupportedContentType);
        result.Items.Select(item => item.RelativeFilePath).Should().Equal("first.jpg", "last.jpg");
        result.Items.Select(item => item.SortOrder).Should().Equal(0, 1);
        await mockMedia.Received(1).PickPhotosAsync(10, Arg.Any<CancellationToken>());
        foreach (var path in paths)
        {
            await mockPreparation.Received(1).PrepareFromFileAsync(UserId, path, "image/jpeg", null,
                PhotographSourceEnum.Gallery, Arg.Any<CancellationToken>());
        }
    }

    [Theory]
    [InlineData(true, PhotographPickOutcomeEnum.PermissionDenied, PhotographPreparationOutcomeEnum.PermissionDenied)]
    [InlineData(false, PhotographPickOutcomeEnum.PermissionDenied, PhotographPreparationOutcomeEnum.PermissionDenied)]
    [InlineData(true, PhotographPickOutcomeEnum.Cancelled, PhotographPreparationOutcomeEnum.Cancelled)]
    [InlineData(false, PhotographPickOutcomeEnum.Cancelled, PhotographPreparationOutcomeEnum.Cancelled)]
    [InlineData(true, PhotographPickOutcomeEnum.Unavailable, PhotographPreparationOutcomeEnum.CouldNotPrepare)]
    [InlineData(false, PhotographPickOutcomeEnum.Unavailable, PhotographPreparationOutcomeEnum.CouldNotPrepare)]
    public async Task ItShouldPreserveExistingPhotosWithoutPreparingFilesWhenPickingDoesNotSucceed(
        bool camera, PhotographPickOutcomeEnum outcome, PhotographPreparationOutcomeEnum expected)
    {
        // Arrange
        var mockMedia = Substitute.For<IPhotographMediaAdapter>();
        var mockPreparation = Substitute.For<IPhotographPreparationService>();
        var pick = new PhotographPickResultModel { Outcome = outcome };
        mockMedia.CapturePhotoAsync(Arg.Any<CancellationToken>()).Returns(pick);
        mockMedia.PickPhotosAsync(9, Arg.Any<CancellationToken>()).Returns(pick);
        var existing = CreateItem(0);
        var sut = new PhotographCollectionEditorService(mockMedia, mockPreparation);

        // Act
        var result = camera
            ? await sut.TakePhotoAsync(UserId, [existing], 10, CancellationToken.None)
            : await sut.ChooseFromGalleryAsync(UserId, [existing], 10, CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(expected);
        result.Items.Should().ContainSingle().Which.Should().BeSameAs(existing);
        await mockMedia.Received(camera ? 1 : 0).CapturePhotoAsync(Arg.Any<CancellationToken>());
        await mockMedia.Received(camera ? 0 : 1).PickPhotosAsync(9, Arg.Any<CancellationToken>());
        await mockPreparation.DidNotReceive().PrepareFromFileAsync(Arg.Any<Guid>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<DateTimeOffset?>(), Arg.Any<PhotographSourceEnum>(), Arg.Any<CancellationToken>());
    }

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
        moved.Items[0].Metadata.Should().Be(second.Metadata);
        moved.Items[1].Metadata.Should().Be(first.Metadata);

        var removed = sut.Remove(moved.Items, second.Id);
        removed.Items.Should().HaveCount(2);
        removed.Items.Select(item => item.SortOrder).Should().Equal(0, 1);
        removed.Items[0].Metadata.Should().Be(first.Metadata);
        removed.Items[1].Metadata.Should().Be(third.Metadata);
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
            ContentType = PhotographContentTypeConstants.Jpeg,
            Metadata = new PhotographMetadataModel(DateTimeOffset.Parse("2026-06-01T10:00:00Z"), 53.3, -6.2)
        };
    }
}
