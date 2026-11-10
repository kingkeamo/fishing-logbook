using FishingLogBook.Mobile.Features.Import.Enums;
using FishingLogBook.Mobile.Features.Import.Models;
using FishingLogBook.Mobile.Features.Import.Services;
using FishingLogBook.Mobile.Features.Photographs.Models;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Import.ViewModels.ImportViewModelTests;

public abstract class BaseImportCatchActionsTest : BaseImportViewModelTest
{
    protected static readonly Guid FirstPhotoId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    protected static readonly Guid SecondPhotoId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    protected static readonly Guid ThirdPhotoId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    protected static ImportSelectedPhotoModel PhotoAt(
        Guid id,
        int index,
        int minutesAfterStart,
        ImportLocationModel? location = null)
    {
        var photo = new ImportSelectedPhotoModel(
            id,
            index,
            "image/jpeg",
            100,
            $"token-{index}",
            $"photo-{index}.jpg",
            $"file://thumb-{index}.jpg");
        photo.SetPreparation(ImportPhotoPreparationStatusEnum.Ready, $"token-{index}", $"file://thumb-{index}.jpg");
        photo.SetMetadata(
            ImportMetadataStatusEnum.Available,
            ImportTimestampModel.FromExplicitInstant(
                new DateTimeOffset(2026, 11, 1, 15, 0, 0, TimeSpan.Zero).AddMinutes(minutesAfterStart),
                ImportTimestampSourceEnum.ExifOriginal),
            location ?? new ImportLocationModel(null, null, false));
        return photo;
    }

    protected async Task ReviewPreparedPhotosAsync(params ImportSelectedPhotoModel[] photos)
    {
        MockMedia.PickPhotosAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(PhotographPickResultModel.FromFiles(
                [.. photos.Select((_, index) => new PhotographPickedFileModel
                {
                    FullPath = $"photo-{index}.jpg",
                    ContentType = "image/jpeg"
                })]));
        MockPreparation.PrepareSelectionAsync(Arg.Any<IReadOnlyList<PhotographPickedFileModel>>(), Arg.Any<CancellationToken>())
            .Returns(photos);
        MockCatchProposals.Propose(Arg.Any<ImportBatchModel>())
            .Returns(callInfo => new ImportCatchProposalService().Propose(callInfo.Arg<ImportBatchModel>()));

        await Sut.LoadAsync(CancellationToken.None);
        await Sut.ContinueAsync(CancellationToken.None);
        await Sut.PickPhotosAsync(CancellationToken.None);
        await Sut.ContinueAsync(CancellationToken.None);
    }
}
