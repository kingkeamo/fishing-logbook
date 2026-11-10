using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Import.Components.ImportPhotographPicker;
using FishingLogBook.Web.Features.Import.Enums;
using FishingLogBook.Web.Features.Import.Models;
using FishingLogBook.Web.Features.Import.Pages.ImportCatchCatalogue;
using FishingLogBook.Web.Features.Import.Services;
using FishingLogBook.Web.Features.Profile.Models;
using FishingLogBook.Web.Features.Profile.Providers;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Import.Pages.ImportCatchCatalogueTests;

public class WhenTestingPickersAndCorrections : BaseImportCatchCatalogueTest
{
    private static IImportCatchProposalService Proposals()
    {
        var proposal = Substitute.For<IImportCatchProposalService>();
        proposal.Propose(Arg.Any<ImportBatchModel>()).Returns(call => ProposalsFor(call.Arg<ImportBatchModel>()));
        return proposal;
    }

    private static async Task SelectDefaultsAsync(IRenderedComponent<ImportCatchCatalogue> cut)
    {
        cut.WaitForElement("#import-batch-details");
        cut.Find("#import-method-Fly").Click();
        cut.Find("#import-species-BrownTrout").Click();
        cut.Find("#import-batch-continue").Click();
        await Task.CompletedTask;
    }

    private static ImportSelectedPhotoModel PreparedAs(int index, ImportPhotoPreparationStatusEnum status)
    {
        var photo = new ImportSelectedPhotoModel(
            Guid.Parse($"66666666-6666-6666-6666-{index + 1:D12}"),
            index,
            "image/jpeg",
            1024,
            null,
            $"failed-{index}.jpg");
        photo.SetPreparation(status);
        return photo;
    }

    [Fact]
    public async Task ItShouldKeepTheBatchMethodWhenTheFullCataloguePickerIsDismissed()
    {
        // Arrange
        var modal = Substitute.For<IModalService>();
        modal.ShowAsync<CataloguePickerModal, CataloguePickerModalModel, CataloguePickerModalResult>(
                Arg.Any<CataloguePickerModalModel>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CataloguePickerModalResult?>(null));
        await using var context = CreateContext(
            Proposals(),
            Substitute.For<IImportPhotoPreparationService>(),
            preferences: Preferences(includeDefaults: false),
            modalService: modal);
        var cut = context.Render<ImportCatchCatalogue>();
        cut.WaitForElement("#import-batch-details");

        // Act
        cut.Find("#import-method-more").Click();
        cut.Find("#import-species-more").Click();

        // Assert
        cut.Find("#import-batch-continue").HasAttribute("disabled").Should().BeTrue();
        await modal.Received(2).ShowAsync<CataloguePickerModal, CataloguePickerModalModel, CataloguePickerModalResult>(
            Arg.Any<CataloguePickerModalModel>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldLeaveTheSpeciesUnchosenWhenTheDefaultMethodHasNoDefaultSpecies()
    {
        // Arrange
        var provider = Substitute.For<IAnglerPreferencesProvider>();
        provider.GetAsync(Arg.Any<CancellationToken>()).Returns(new AnglerPreferencesModel(
            new FishingCatalogueDto(
                [new FishingMethodDto(MethodId, "Fly", "Fly")],
                [new SpeciesDto(SpeciesId, "BrownTrout", "Brown Trout")]),
            new FishingPreferencesDto(
            [
                new FishingMethodPreferenceDto(
                    MethodId,
                    "Fly",
                    "Fly",
                    true,
                    [new FishingSpeciesPreferenceDto(SpeciesId, "BrownTrout", "Brown Trout", false)])
            ]),
            FishingLogBook.Shared.Enums.WeightUnitEnum.Kg,
            FishingLogBook.Shared.Enums.LengthUnitEnum.Cm));
        await using var context = CreateContext(
            Proposals(),
            Substitute.For<IImportPhotoPreparationService>(),
            preferences: provider);

        // Act
        var cut = context.Render<ImportCatchCatalogue>();
        cut.WaitForElement("#import-batch-details");

        // Assert
        cut.Find("#import-batch-continue").HasAttribute("disabled").Should().BeTrue();
        await provider.Received(1).GetAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(ImportPhotoPreparationStatusEnum.UnsupportedType, "Import_PhotoUnsupported")]
    [InlineData(ImportPhotoPreparationStatusEnum.TooLarge, "Import_PhotoTooLarge")]
    [InlineData(ImportPhotoPreparationStatusEnum.Cancelled, "Import_PhotoCancelled")]
    [InlineData(ImportPhotoPreparationStatusEnum.PreparationFailed, "Import_PhotoFailed")]
    public async Task ItShouldExplainWhyAPhotographCouldNotBePrepared(
        ImportPhotoPreparationStatusEnum status,
        string resourceKey)
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext(Proposals(), Substitute.For<IImportPhotoPreparationService>());
        var cut = context.Render<ImportCatchCatalogue>();
        await SelectDefaultsAsync(cut);
        var expected = context.Services.GetRequiredService<IStringLocalizer<UiStrings>>()[resourceKey].Value;

        // Act
        await cut.InvokeAsync(() => cut.FindComponent<ImportPhotographPicker>().Instance.PhotosPrepared
            .InvokeAsync([PreparedAs(0, status)]));

        // Assert
        cut.Find("#import-photo-0").TextContent.Should().Contain(expected);
    }

    [Fact]
    public async Task ItShouldRemoveACatchAndReleaseItsPhotographsWhenTheAnglerConfirms()
    {
        // Arrange
        var modal = SelectingModal(MethodId, SpeciesId);
        modal.ConfirmAsync(Arg.Any<ConfirmModalModel>(), Arg.Any<CancellationToken>()).Returns(true);
        var preparation = Substitute.For<IImportPhotoPreparationService>();
        await using var context = CreateContext(Proposals(), preparation, modalService: modal);
        var cut = context.Render<ImportCatchCatalogue>();
        await SelectDefaultsAsync(cut);
        var photo = ReadyPhoto(0);
        await cut.InvokeAsync(() => cut.FindComponent<ImportPhotographPicker>().Instance.PhotosPrepared
            .InvokeAsync([photo, ReadyPhoto(1)]));
        cut.Find("#import-photos-continue").Click();
        cut.Find("#import-catch-1-edit").Click();

        // Act
        cut.Find("#import-catch-1-remove").Click();

        // Assert
        cut.WaitForAssertion(() => cut.FindAll("[id^='import-catch-'][id$='-status']").Should().HaveCount(1));
        await preparation.Received(1).RemoveAsync(
            Arg.Is<ImportSelectedPhotoModel>(removed => removed.Id == photo.Id),
            Arg.Any<CancellationToken>());
        await modal.Received(1).ConfirmAsync(Arg.Any<ConfirmModalModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepTheCatchWhenTheAnglerDeclinesToRemoveIt()
    {
        // Arrange
        var modal = SelectingModal(MethodId, SpeciesId);
        modal.ConfirmAsync(Arg.Any<ConfirmModalModel>(), Arg.Any<CancellationToken>()).Returns(false);
        var preparation = Substitute.For<IImportPhotoPreparationService>();
        await using var context = CreateContext(Proposals(), preparation, modalService: modal);
        var cut = context.Render<ImportCatchCatalogue>();
        await SelectDefaultsAsync(cut);
        await cut.InvokeAsync(() => cut.FindComponent<ImportPhotographPicker>().Instance.PhotosPrepared
            .InvokeAsync([ReadyPhoto(0), ReadyPhoto(1)]));
        cut.Find("#import-photos-continue").Click();
        cut.Find("#import-catch-1-edit").Click();

        // Act
        cut.Find("#import-catch-1-remove").Click();

        // Assert
        cut.FindAll("[id^='import-catch-'][id$='-status']").Should().HaveCount(2);
        await preparation.DidNotReceive().RemoveAsync(
            Arg.Any<ImportSelectedPhotoModel>(),
            Arg.Any<CancellationToken>());
        await modal.Received(1).ConfirmAsync(Arg.Any<ConfirmModalModel>(), Arg.Any<CancellationToken>());
    }
}
