using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Import.Components.ImportCatchReviewCard;
using FishingLogBook.Web.Features.Import.Enums;
using FishingLogBook.Web.Features.Import.Models;
using FishingLogBook.Web.Features.Profile.Models;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Import.Components.ImportCatchReviewCardTests;

public class BaseImportCatchReviewCardTest
{
    protected static readonly Guid FlyId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    protected static readonly Guid SpinningId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    protected static readonly Guid BrownTroutId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    protected static readonly Guid PikeId = Guid.Parse("cccccccc-0000-0000-0000-000000000002");
    protected static readonly DateTimeOffset CapturedOn = DateTimeOffset.Parse("2025-06-14T09:30:00+01:00");

    protected static BunitContext CreateContext(IModalService? modal = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton<IMeasurementService, MeasurementService>();
        context.Services.AddSingleton(modal ?? Substitute.For<IModalService>());
        var time = Substitute.For<ITimeService>();
        time.FromDateTimeLocalValueAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(CapturedOn);
        context.Services.AddSingleton(time);
        return context;
    }

    protected static ImportSelectedPhotoModel Photo(int index = 0)
    {
        var photo = new ImportSelectedPhotoModel(
            Guid.Parse($"11111111-1111-1111-1111-{index + 1:D12}"),
            index,
            "image/jpeg",
            1024,
            "token",
            $"catch-{index}.jpg",
            $"blob:thumbnail-{index}");
        photo.SetPreparation(ImportPhotoPreparationStatusEnum.Ready, "token", $"blob:thumbnail-{index}");
        photo.SetMetadata(
            ImportMetadataStatusEnum.Available,
            ImportTimestampModel.FromExplicitInstant(CapturedOn.AddMinutes(index), ImportTimestampSourceEnum.ExifOriginal),
            new ImportLocationModel(53.3498, -6.2603, true));
        return photo;
    }

    protected static ImportCatchProposalModel Proposal(Guid id, params ImportSelectedPhotoModel[] photos)
    {
        return new ImportCatchProposalModel(
            id,
            photos.Select(photo => photo.Id).ToArray(),
            photos[0].Timestamp,
            new ImportCatalogueSelectionModel(FlyId, "Fly", "Fly"),
            new ImportCatalogueSelectionModel(BrownTroutId, "BrownTrout", "Brown Trout"),
            photos[0].Location.Accept(),
            [ImportCatchProposalReasonEnum.TrustworthyCaptureTime]);
    }

    protected static ImportBatchModel Batch(
        IReadOnlyList<ImportSelectedPhotoModel> photos,
        params ImportCatchProposalModel[] proposals)
    {
        var batch = new ImportBatchModel(
            Guid.NewGuid(),
            new ImportCatalogueSelectionModel(FlyId, "Fly", "Fly"),
            new ImportCatalogueSelectionModel(BrownTroutId, "BrownTrout", "Brown Trout"));
        foreach (var photo in photos)
        {
            batch.AddPhoto(photo);
        }

        foreach (var proposal in proposals)
        {
            batch.AddCatchProposal(proposal);
        }

        return batch;
    }

    protected static AnglerPreferencesModel Preferences(
        WeightUnitEnum weightUnit = WeightUnitEnum.Kg,
        LengthUnitEnum lengthUnit = LengthUnitEnum.Cm)
    {
        return new AnglerPreferencesModel(
            new FishingCatalogueDto(
                [new FishingMethodDto(FlyId, "Fly", "Fly"), new FishingMethodDto(SpinningId, "Spinning", "Spinning")],
                [new SpeciesDto(BrownTroutId, "BrownTrout", "Brown Trout"), new SpeciesDto(PikeId, "Pike", "Pike")]),
            new FishingPreferencesDto(
            [
                new FishingMethodPreferenceDto(
                    FlyId,
                    "Fly",
                    "Fly",
                    true,
                    [new FishingSpeciesPreferenceDto(BrownTroutId, "BrownTrout", "Brown Trout", true)]),
                new FishingMethodPreferenceDto(
                    SpinningId,
                    "Spinning",
                    "Spinning",
                    false,
                    [new FishingSpeciesPreferenceDto(PikeId, "Pike", "Pike", true)])
            ]),
            weightUnit,
            lengthUnit);
    }

    protected static IRenderedComponent<ImportCatchReviewCard> Render(
        BunitContext context,
        ImportBatchModel batch,
        ImportCatchProposalModel proposal,
        AnglerPreferencesModel preferences,
        Action? changed = null,
        Action<Guid>? removeCatch = null,
        bool editable = true)
    {
        return context.Render<ImportCatchReviewCard>(parameters => parameters
            .Add(component => component.Proposal, proposal)
            .Add(component => component.Batch, batch)
            .Add(component => component.Preferences, preferences)
            .Add(component => component.Number, 1)
            .Add(component => component.Editable, editable)
            .Add(component => component.Changed, Microsoft.AspNetCore.Components.EventCallback.Factory.Create(
                new object(),
                () => changed?.Invoke()))
            .Add(component => component.RemoveCatch, Microsoft.AspNetCore.Components.EventCallback.Factory.Create<Guid>(
                new object(),
                id => removeCatch?.Invoke(id))));
    }
}
