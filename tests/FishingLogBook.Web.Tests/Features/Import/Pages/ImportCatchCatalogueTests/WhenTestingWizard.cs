using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Import.Components.ImportCatchReviewCard;
using FishingLogBook.Web.Features.Import.Components.ImportPhotographPicker;
using FishingLogBook.Web.Features.Import.Enums;
using FishingLogBook.Web.Features.Import.Models;
using FishingLogBook.Web.Features.Import.Pages.ImportCatchCatalogue;
using FishingLogBook.Web.Features.Import.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Import.Pages.ImportCatchCatalogueTests;

public class WhenTestingWizard : BaseImportCatchCatalogueTest
{
    [Fact]
    public async Task ItShouldEnterTheWizardAtBatchDetailsFromTheImportRoute()
    {
        // Arrange
        var route = typeof(ImportCatchCatalogue).GetCustomAttributes(typeof(RouteAttribute), inherit: false)
            .Cast<RouteAttribute>()
            .Single();
        await using var context = CreateContext(
            Substitute.For<IImportCatchProposalService>(),
            Substitute.For<IImportPhotoPreparationService>());

        // Act
        var cut = context.Render<ImportCatchCatalogue>();
        var batchDetails = cut.WaitForElement("#import-batch-details");

        // Assert
        route.Template.Should().Be("/import");
        batchDetails.Should().NotBeNull();
        cut.FindAll("#import-photo-selection").Should().BeEmpty();
        cut.FindAll("#import-catch-review").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldRequireBothBatchDefaultsBeforeContinuing()
    {
        // Arrange
        var proposal = Substitute.For<IImportCatchProposalService>();
        var preparation = Substitute.For<IImportPhotoPreparationService>();
        var modal = SelectingModal(MethodId, SpeciesId);
        await using var context = CreateContext(
            proposal,
            preparation,
            preferences: Preferences(includeDefaults: false),
            modalService: modal);
        var cut = context.Render<ImportCatchCatalogue>();
        cut.WaitForElement("#import-batch-details");

        // Act
        cut.Find("#import-method-more").Click();

        // Assert
        cut.Find("#import-batch-continue").HasAttribute("disabled").Should().BeTrue();
        cut.Find("#import-species-more").Click();
        cut.Find("#import-batch-continue").HasAttribute("disabled").Should().BeFalse();
        await modal.Received(1).ShowAsync<CataloguePickerModal, CataloguePickerModalModel, CataloguePickerModalResult>(
            Arg.Is<CataloguePickerModalModel>(model =>
                model.Title == "Fishing method"
                && model.Options.Any(option => option.Id == MethodId)),
            Arg.Any<CancellationToken>());
        await modal.Received(1).ShowAsync<CataloguePickerModal, CataloguePickerModalModel, CataloguePickerModalResult>(
            Arg.Is<CataloguePickerModalModel>(model =>
                model.Title == "Species"
                && model.Options.Any(option => option.Id == SpeciesId)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldApplyConfiguredDefaultsAndKeepThemChangeable()
    {
        // Arrange
        await using var context = CreateContext(
            Substitute.For<IImportCatchProposalService>(),
            Substitute.For<IImportPhotoPreparationService>(),
            modalService: SelectingModal(SecondMethodId));

        // Act
        var cut = context.Render<ImportCatchCatalogue>();
        cut.WaitForElement("#import-batch-details");

        // Assert
        cut.Find("#import-method-Fly").ClassList.Should().Contain("mud-chip-filled");
        cut.FindAll("#import-method-Lure").Should().BeEmpty();
        cut.Find("#import-species-BrownTrout").ClassList.Should().Contain("mud-chip-filled");
        cut.Find("#import-batch-continue").HasAttribute("disabled").Should().BeFalse();

        // Act
        cut.Find("#import-method-more").Click();

        // Assert
        cut.Find("#import-method-Lure").ClassList.Should().Contain("mud-chip-filled");
        cut.Find("#import-batch-continue").HasAttribute("disabled").Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldReuseThePickerAndPreventReviewWithoutAUsablePhoto()
    {
        // Arrange
        var proposal = Substitute.For<IImportCatchProposalService>();
        var preparation = Substitute.For<IImportPhotoPreparationService>();
        await using var context = CreateContext(proposal, preparation);
        var cut = context.Render<ImportCatchCatalogue>();
        await SelectDefaultsAndContinueAsync(cut);

        // Act
        await cut.InvokeAsync(() => cut.FindComponent<ImportPhotographPicker>().Instance.PhotosPrepared
            .InvokeAsync([FailedPhoto(0)]));

        // Assert
        cut.Find("#import-photo-0").TextContent.Should().Contain("not supported");
        cut.Find("#import-photos-continue").HasAttribute("disabled").Should().BeTrue();
        proposal.DidNotReceive().Propose(Arg.Any<ImportBatchModel>());
    }

    [Fact]
    public async Task ItShouldKeepPreparedPhotosUsableWhenHistoricalMetadataIsUnavailable()
    {
        // Arrange
        var proposal = Substitute.For<IImportCatchProposalService>();
        var preparation = Substitute.For<IImportPhotoPreparationService>();
        await using var context = CreateContext(proposal, preparation);
        var cut = context.Render<ImportCatchCatalogue>();
        await SelectDefaultsAndContinueAsync(cut);
        var photo = ReadyPhoto(0, ImportTimestampModel.Missing(), ImportMetadataStatusEnum.Unavailable);

        // Act
        await cut.InvokeAsync(() => cut.FindComponent<ImportPhotographPicker>().Instance.PhotosPrepared
            .InvokeAsync([photo]));

        // Assert
        cut.Find("#import-photo-0").TextContent.Should().Contain("historical details need review");
        cut.Find("#import-photos-continue").HasAttribute("disabled").Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldShowProcessingStateAndPreventProgressionWhilePreparing()
    {
        // Arrange
        var proposal = Substitute.For<IImportCatchProposalService>();
        var preparation = Substitute.For<IImportPhotoPreparationService>();
        await using var context = CreateContext(proposal, preparation);
        var cut = context.Render<ImportCatchCatalogue>();
        await SelectDefaultsAndContinueAsync(cut);

        // Act
        await cut.InvokeAsync(() => cut.FindComponent<ImportPhotographPicker>().Instance.SelectionStarted.InvokeAsync());

        // Assert
        cut.Find("#import-processing").Should().NotBeNull();
        cut.Find("#import-photos-continue").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldLocaliseTheWizardInFrench()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        var proposal = Substitute.For<IImportCatchProposalService>();
        var preparation = Substitute.For<IImportPhotoPreparationService>();
        await using var context = CreateContext(proposal, preparation);

        // Act
        var cut = context.Render<ImportCatchCatalogue>();
        cut.WaitForElement("#import-batch-details");

        // Assert
        cut.Find("#import-title").TextContent.Should().Contain("Cataloguer des prises historiques");
        cut.Find("#import-method-chips").TextContent.Should().Contain("Fly");
        cut.Find("#import-method-more").TextContent.Should().Contain("Plus");
    }

    [Fact]
    public async Task ItShouldGenerateProposalsThroughTheGroupingBoundaryAndRenderEachMembership()
    {
        // Arrange
        var proposal = Substitute.For<IImportCatchProposalService>();
        proposal.Propose(Arg.Any<ImportBatchModel>()).Returns(call => ProposalsFor(call.Arg<ImportBatchModel>()));
        var preparation = Substitute.For<IImportPhotoPreparationService>();
        await using var context = CreateContext(proposal, preparation);
        var cut = context.Render<ImportCatchCatalogue>();
        await SelectDefaultsAndContinueAsync(cut);
        var photos = new[] { ReadyPhoto(0), ReadyPhoto(1) };
        await cut.InvokeAsync(() => cut.FindComponent<ImportPhotographPicker>().Instance.PhotosPrepared
            .InvokeAsync(photos));

        // Act
        cut.Find("#import-photos-continue").Click();

        // Assert
        cut.FindComponents<ImportCatchReviewCard>().Should().HaveCount(2);
        var reviewButton = cut.Find("#import-catch-1-edit");
        reviewButton.TextContent.Should().Contain("Review");
        reviewButton.ClassList.Should().Contain("mud-button-filled-primary");
        proposal.Received(1).Propose(Arg.Is<ImportBatchModel>(batch =>
            batch.FishingMethod.Id == MethodId
            && batch.Species.Id == SpeciesId
            && batch.Photos.Count == 2));
    }

    [Fact]
    public async Task ItShouldShowGroupedPhotosTogetherAndExcludeFailedPhotosFromMembership()
    {
        // Arrange
        var proposal = Substitute.For<IImportCatchProposalService>();
        proposal.Propose(Arg.Any<ImportBatchModel>()).Returns(call =>
        {
            var batch = call.Arg<ImportBatchModel>();
            var ready = batch.Photos.Where(photo => photo.IsReady).ToArray();
            return
            [
                new ImportCatchProposalModel(
                    Guid.NewGuid(), ready.Select(photo => photo.Id), ready[0].Timestamp,
                    batch.FishingMethod, batch.Species,
                    reasons: [FishingLogBook.Web.Features.Import.Enums.ImportCatchProposalReasonEnum.TrustworthyCaptureTime])
            ];
        });
        var preparation = Substitute.For<IImportPhotoPreparationService>();
        await using var context = CreateContext(proposal, preparation);
        var cut = context.Render<ImportCatchCatalogue>();
        await SelectDefaultsAndContinueAsync(cut);
        await cut.InvokeAsync(() => cut.FindComponent<ImportPhotographPicker>().Instance.PhotosPrepared
            .InvokeAsync([ReadyPhoto(0), ReadyPhoto(1), FailedPhoto(2)]));

        // Act
        cut.Find("#import-photos-continue").Click();

        // Assert
        cut.FindComponents<ImportCatchReviewCard>().Should().ContainSingle();
        cut.FindAll("#import-catch-1-photos img").Should().HaveCount(2);
    }

    [Fact]
    public async Task ItShouldPreserveStateOnBackAndRegenerateWithoutDuplicates()
    {
        // Arrange
        var proposal = Substitute.For<IImportCatchProposalService>();
        proposal.Propose(Arg.Any<ImportBatchModel>()).Returns(call => ProposalsFor(call.Arg<ImportBatchModel>()));
        var preparation = Substitute.For<IImportPhotoPreparationService>();
        await using var context = CreateContext(proposal, preparation);
        var cut = context.Render<ImportCatchCatalogue>();
        await SelectDefaultsAndContinueAsync(cut);
        await cut.InvokeAsync(() => cut.FindComponent<ImportPhotographPicker>().Instance.PhotosPrepared
            .InvokeAsync([ReadyPhoto(0)]));
        cut.Find("#import-photos-continue").Click();

        // Act
        cut.Find("#import-review-back").Click();
        cut.Find("#import-photos-continue").Click();

        // Assert
        cut.FindComponents<ImportCatchReviewCard>().Should().ContainSingle();
        proposal.Received(2).Propose(Arg.Any<ImportBatchModel>());
    }

    [Fact]
    public async Task ItShouldKeepThumbnailResourcesWhenReturningFromReview()
    {
        // Arrange
        var proposal = Substitute.For<IImportCatchProposalService>();
        proposal.Propose(Arg.Any<ImportBatchModel>()).Returns(call => ProposalsFor(call.Arg<ImportBatchModel>()));
        var preparation = Substitute.For<IImportPhotoPreparationService>();
        await using var context = CreateContext(proposal, preparation);
        var cut = context.Render<ImportCatchCatalogue>();
        await SelectDefaultsAndContinueAsync(cut);
        var photo = ReadyPhoto(0);
        await cut.InvokeAsync(() => cut.FindComponent<ImportPhotographPicker>().Instance.PhotosPrepared
            .InvokeAsync([photo]));
        cut.Find("#import-photos-continue").Click();

        // Act
        cut.Find("#import-review-back").Click();

        // Assert
        cut.Find("#import-photo-0 img").GetAttribute("src").Should().Be(photo.ThumbnailUrl);
        await preparation.DidNotReceive().ClearAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldFastConfirmReadyCatchesAndReachTheTripReviewBoundary()
    {
        // Arrange
        var proposal = Substitute.For<IImportCatchProposalService>();
        proposal.Propose(Arg.Any<ImportBatchModel>()).Returns(call => ProposalsFor(call.Arg<ImportBatchModel>()));
        await using var context = CreateContext(proposal, Substitute.For<IImportPhotoPreparationService>());
        var cut = context.Render<ImportCatchCatalogue>();
        await SelectDefaultsAndContinueAsync(cut);
        await cut.InvokeAsync(() => cut.FindComponent<ImportPhotographPicker>().Instance.PhotosPrepared
            .InvokeAsync([ReadyPhoto(0)]));
        cut.Find("#import-photos-continue").Click();

        // Act
        cut.Find("#import-catch-1-confirm").Click();

        // Assert
        cut.Find("#import-catch-1-status").TextContent.Should().Contain("Reviewed");
        cut.Find("#import-review-continue").HasAttribute("disabled").Should().BeFalse();

        // Act
        cut.Find("#import-review-continue").Click();

        // Assert
        cut.Find("#import-trip-review").Should().NotBeNull();
        cut.Find("#import-trip-none").Should().NotBeNull();
        cut.Find("#import-trip-continue").HasAttribute("disabled").Should().BeFalse();

        // Act
        cut.Find("#import-trip-continue").Click();

        // Assert
        cut.Find("#import-confirmation-boundary").Should().NotBeNull();
    }

    [Fact]
    public async Task ItShouldOverrideOneCatchThroughTheExistingCataloguePickerWithoutChangingBatchDefaults()
    {
        // Arrange
        ImportCatchProposalModel? created = null;
        var proposal = Substitute.For<IImportCatchProposalService>();
        proposal.Propose(Arg.Any<ImportBatchModel>()).Returns(call =>
        {
            var batch = call.Arg<ImportBatchModel>();
            created = ProposalsFor(batch).Single();
            return [created];
        });
        var modal = SelectingModal(SecondMethodId);
        await using var context = CreateContext(
            proposal, Substitute.For<IImportPhotoPreparationService>(), modalService: modal);
        var cut = context.Render<ImportCatchCatalogue>();
        await SelectDefaultsAndContinueAsync(cut);
        await cut.InvokeAsync(() => cut.FindComponent<ImportPhotographPicker>().Instance.PhotosPrepared
            .InvokeAsync([ReadyPhoto(0)]));
        cut.Find("#import-photos-continue").Click();
        cut.Find("#import-catch-1-edit").Click();

        // Act
        cut.Find("#import-catch-1-method-more").Click();

        // Assert
        created!.Method.Id.Should().Be(SecondMethodId);
        created.InheritedMethod.Id.Should().Be(MethodId);
        cut.Find("#import-catch-1-method").TextContent.Should().Contain("Lure");
        await modal.Received(1).ShowAsync<CataloguePickerModal, CataloguePickerModalModel, CataloguePickerModalResult>(
            Arg.Is<CataloguePickerModalModel>(model => model.Options.Any(option => option.Id == SecondMethodId)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSplitAndMergePhotosWithoutRerunningAutomaticGrouping()
    {
        // Arrange
        var proposal = Substitute.For<IImportCatchProposalService>();
        proposal.Propose(Arg.Any<ImportBatchModel>()).Returns(call =>
        {
            var batch = call.Arg<ImportBatchModel>();
            return [new ImportCatchProposalModel(
                Guid.NewGuid(), batch.Photos.Select(photo => photo.Id), batch.Photos[0].Timestamp,
                batch.FishingMethod, batch.Species,
                reasons: [ImportCatchProposalReasonEnum.TrustworthyCaptureTime])];
        });
        await using var context = CreateContext(proposal, Substitute.For<IImportPhotoPreparationService>());
        var cut = context.Render<ImportCatchCatalogue>();
        await SelectDefaultsAndContinueAsync(cut);
        await cut.InvokeAsync(() => cut.FindComponent<ImportPhotographPicker>().Instance.PhotosPrepared
            .InvokeAsync([ReadyPhoto(0), ReadyPhoto(1)]));
        cut.Find("#import-photos-continue").Click();
        cut.Find("#import-catch-1-edit").Click();
        cut.Find("#import-catch-1-select-1").Change(true);

        // Act
        cut.Find("#import-catch-1-split").Click();

        // Assert
        cut.FindComponents<ImportCatchReviewCard>().Should().HaveCount(2);
        proposal.Received(1).Propose(Arg.Any<ImportBatchModel>());

        // Act
        cut.Find("#import-catch-1-merge-2").Click();

        // Assert
        cut.FindComponents<ImportCatchReviewCard>().Should().ContainSingle();
        cut.FindAll("#import-catch-1-photos img").Should().HaveCount(2);
        proposal.Received(1).Propose(Arg.Any<ImportBatchModel>());
    }

    [Fact]
    public async Task ItShouldReleaseSelectedPhotoResourcesDuringCorrection()
    {
        // Arrange
        var proposal = Substitute.For<IImportCatchProposalService>();
        proposal.Propose(Arg.Any<ImportBatchModel>()).Returns(call => ProposalsFor(call.Arg<ImportBatchModel>()));
        var preparation = Substitute.For<IImportPhotoPreparationService>();
        await using var context = CreateContext(proposal, preparation);
        var cut = context.Render<ImportCatchCatalogue>();
        await SelectDefaultsAndContinueAsync(cut);
        var photo = ReadyPhoto(0);
        await cut.InvokeAsync(() => cut.FindComponent<ImportPhotographPicker>().Instance.PhotosPrepared.InvokeAsync([photo]));
        cut.Find("#import-photos-continue").Click();
        cut.Find("#import-catch-1-edit").Click();
        cut.Find("#import-catch-1-select-0").Change(true);

        // Act
        cut.Find("#import-catch-1-remove-photos").Click();

        // Assert
        await preparation.Received(1).RemoveAsync(photo, Arg.Any<CancellationToken>());
        cut.FindComponents<ImportCatchReviewCard>().Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldRegenerateWithChangedDefaultsAndChangedPhotos()
    {
        // Arrange
        var proposal = Substitute.For<IImportCatchProposalService>();
        proposal.Propose(Arg.Any<ImportBatchModel>()).Returns(call => ProposalsFor(call.Arg<ImportBatchModel>()));
        var preparation = Substitute.For<IImportPhotoPreparationService>();
        await using var context = CreateContext(
            proposal,
            preparation,
            modalService: SelectingModal(SecondMethodId));
        var cut = context.Render<ImportCatchCatalogue>();
        await SelectDefaultsAndContinueAsync(cut);
        await cut.InvokeAsync(() => cut.FindComponent<ImportPhotographPicker>().Instance.PhotosPrepared
            .InvokeAsync([ReadyPhoto(0)]));
        cut.Find("#import-photos-back").Click();
        cut.Find("#import-method-more").Click();
        cut.Find("#import-batch-continue").Click();
        await cut.InvokeAsync(() => cut.FindComponent<ImportPhotographPicker>().Instance.PhotosPrepared
            .InvokeAsync([ReadyPhoto(1)]));

        // Act
        cut.Find("#import-photos-continue").Click();

        // Assert
        proposal.Received(1).Propose(Arg.Is<ImportBatchModel>(batch =>
            batch.FishingMethod.Id == SecondMethodId
            && batch.Photos.Count == 1
            && batch.Photos[0].SelectionIndex == 1));
        cut.Find("#import-catch-1").TextContent.Should().Contain("Lure");
    }

    [Fact]
    public async Task ItShouldRemovePhotosThroughThePreparationLifecycle()
    {
        // Arrange
        var proposal = Substitute.For<IImportCatchProposalService>();
        var preparation = Substitute.For<IImportPhotoPreparationService>();
        await using var context = CreateContext(proposal, preparation);
        var cut = context.Render<ImportCatchCatalogue>();
        await SelectDefaultsAndContinueAsync(cut);
        var photo = ReadyPhoto(0);
        await cut.InvokeAsync(() => cut.FindComponent<ImportPhotographPicker>().Instance.PhotosPrepared
            .InvokeAsync([photo]));

        // Act
        cut.Find("#import-remove-photo-0").Click();

        // Assert
        await preparation.Received(1).RemoveAsync(photo, Arg.Any<CancellationToken>());
        cut.FindAll("#import-photo-0").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldClearTransientPhotoResourcesWhenDisposed()
    {
        // Arrange
        var proposal = Substitute.For<IImportCatchProposalService>();
        var preparation = Substitute.For<IImportPhotoPreparationService>();
        preparation.ClearAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        await using var context = CreateContext(proposal, preparation);
        var cut = context.Render<ImportCatchCatalogue>();
        cut.WaitForElement("#import-batch-details");

        // Act
        await cut.Instance.DisposeAsync();

        // Assert
        await preparation.Received(1).ClearAsync(CancellationToken.None);
    }

    private static Task SelectDefaultsAndContinueAsync(IRenderedComponent<ImportCatchCatalogue> cut)
    {
        cut.WaitForElement("#import-batch-details");
        cut.Find("#import-method-Fly").Click();
        cut.Find("#import-species-BrownTrout").Click();
        cut.Find("#import-batch-continue").Click();
        return Task.CompletedTask;
    }
}
