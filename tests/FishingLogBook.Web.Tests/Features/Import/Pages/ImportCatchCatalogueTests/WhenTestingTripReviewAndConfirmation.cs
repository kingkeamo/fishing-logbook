using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Common.Modals.AnglerPicker;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Import.Components.ImportPhotographPicker;
using FishingLogBook.Web.Features.Import.Enums;
using FishingLogBook.Web.Features.Import.Models;
using FishingLogBook.Web.Features.Import.Pages.ImportCatchCatalogue;
using FishingLogBook.Web.Features.Import.Services;
using FishingLogBook.Web.Features.Trips.Components.TripSelector;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Import.Pages.ImportCatchCatalogueTests;

public class WhenTestingTripReviewAndConfirmation : BaseImportCatchCatalogueTest
{
    private static readonly Guid AnglerId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    private static IImportCatchProposalService Proposals()
    {
        var proposal = Substitute.For<IImportCatchProposalService>();
        proposal.Propose(Arg.Any<ImportBatchModel>()).Returns(call => ProposalsFor(call.Arg<ImportBatchModel>()));
        return proposal;
    }

    private static async Task ReachCatchReviewAsync(
        IRenderedComponent<ImportCatchCatalogue> cut,
        params ImportSelectedPhotoModel[] photos)
    {
        cut.WaitForElement("#import-batch-details");
        cut.Find("#import-method-Fly").Click();
        cut.Find("#import-species-BrownTrout").Click();
        cut.Find("#import-batch-continue").Click();
        await cut.InvokeAsync(() => cut.FindComponent<ImportPhotographPicker>().Instance.PhotosPrepared
            .InvokeAsync(photos.Length == 0 ? [ReadyPhoto(0)] : photos));
        cut.Find("#import-photos-continue").Click();
    }

    private static void ConfirmEveryCatch(IRenderedComponent<ImportCatchCatalogue> cut, int count)
    {
        for (var catchNumber = 1; catchNumber <= count; catchNumber++)
        {
            cut.Find($"#import-catch-{catchNumber}-confirm").Click();
        }
    }

    private static async Task ReachTripReviewAsync(
        IRenderedComponent<ImportCatchCatalogue> cut,
        params ImportSelectedPhotoModel[] photos)
    {
        await ReachCatchReviewAsync(cut, photos);
        ConfirmEveryCatch(cut, photos.Length == 0 ? 1 : photos.Length);
        cut.Find("#import-review-continue").Click();
        cut.WaitForElement("#import-trip-review");
    }

    private static async Task ReachConfirmationAsync(IRenderedComponent<ImportCatchCatalogue> cut)
    {
        await ReachTripReviewAsync(cut);
        cut.Find("#import-trip-separate-1").Click();
        cut.Find("#import-trip-continue").Click();
    }

    [Fact]
    public async Task ItShouldStepBackThroughEveryStageWithoutLosingTheWizard()
    {
        // Arrange
        await using var context = CreateContext(Proposals(), Substitute.For<IImportPhotoPreparationService>());
        var cut = context.Render<ImportCatchCatalogue>();
        await ReachConfirmationAsync(cut);

        // Act
        cut.Find("#import-confirm-back").Click();

        // Assert
        cut.Find("#import-catch-review").Should().NotBeNull();

        // Act
        cut.Find("#import-review-continue").Click();
        cut.Find("#import-trip-back").Click();

        // Assert
        cut.Find("#import-catch-review").Should().NotBeNull();

        // Act
        cut.Find("#import-review-back").Click();

        // Assert
        cut.Find("#import-choose-photos").Should().NotBeNull();

        // Act
        cut.Find("#import-photos-back").Click();

        // Assert
        cut.Find("#import-batch-details").Should().NotBeNull();
    }

    [Fact]
    public async Task ItShouldCountANewTripAndOfferParticipantsOnlyWhenTheTripIsCreated()
    {
        // Arrange
        await using var context = CreateContext(Proposals(), Substitute.For<IImportPhotoPreparationService>());
        var cut = context.Render<ImportCatchCatalogue>();
        await ReachTripReviewAsync(cut);

        // Act
        cut.Find("#import-trip-separate-1").Click();

        // Assert
        cut.FindAll("#import-trip-participants-1").Should().BeEmpty();

        // Act
        cut.Find("#import-trip-create-1").Click();

        // Assert
        cut.Find("#import-trip-participants-1").Should().NotBeNull();

        // Act
        cut.Find("#import-trip-continue").Click();

        // Assert
        cut.Find("#import-summary-new-trips").TextContent.Should().Contain("1");
        cut.Find("#import-summary-standalone").TextContent.Should().Contain("0");
    }

    [Fact]
    public async Task ItShouldAddAndRemoveATripParticipantAndNameThemByDisplayNameOrEmail()
    {
        // Arrange
        var modal = SelectingModal(MethodId, SpeciesId);
        var anglers = new Queue<AnglerSummaryDto>(
        [
            new AnglerSummaryDto(AnglerId, "Mary", null, null, "mary@example.test"),
            new AnglerSummaryDto(Guid.Parse("77777777-7777-7777-7777-777777777777"), null, null, null, "tom@example.test"),
            new AnglerSummaryDto(Guid.Parse("88888888-8888-8888-8888-888888888888"), null, null, null, null)
        ]);
        modal.ShowAsync<AnglerPickerModal, AnglerPickerModalModel, AnglerPickerModalResult>(
                Arg.Any<AnglerPickerModalModel>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => new AnglerPickerModalResult(anglers.Dequeue()));
        await using var context = CreateContext(
            Proposals(),
            Substitute.For<IImportPhotoPreparationService>(),
            modalService: modal);
        var cut = context.Render<ImportCatchCatalogue>();
        await ReachTripReviewAsync(cut);
        cut.Find("#import-trip-create-1").Click();

        // Act
        cut.Find("#import-trip-add-participant-1").Click();
        cut.Find("#import-trip-add-participant-1").Click();
        cut.Find("#import-trip-add-participant-1").Click();

        // Assert
        var participants = cut.Find("#import-trip-participants-1").TextContent;
        participants.Should().Contain("Mary").And.Contain("tom@example.test");
        cut.FindAll("[id^='import-trip-participant-']").Should().HaveCount(3);
        await modal.Received(3).ShowAsync<AnglerPickerModal, AnglerPickerModalModel, AnglerPickerModalResult>(
            Arg.Any<AnglerPickerModalModel>(),
            Arg.Any<CancellationToken>());

        // Act
        cut.Find($"#import-trip-participant-{AnglerId:D} .mud-chip-close-button").Click();

        // Assert
        cut.FindAll($"#import-trip-participant-{AnglerId:D}").Should().BeEmpty();
        cut.FindAll("[id^='import-trip-participant-']").Should().HaveCount(2);
    }

    [Fact]
    public async Task ItShouldNotAddAParticipantWhenThePickerIsDismissed()
    {
        // Arrange
        var modal = SelectingModal(MethodId, SpeciesId);
        modal.ShowAsync<AnglerPickerModal, AnglerPickerModalModel, AnglerPickerModalResult>(
                Arg.Any<AnglerPickerModalModel>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AnglerPickerModalResult?>(null));
        await using var context = CreateContext(
            Proposals(),
            Substitute.For<IImportPhotoPreparationService>(),
            modalService: modal);
        var cut = context.Render<ImportCatchCatalogue>();
        await ReachTripReviewAsync(cut);
        cut.Find("#import-trip-create-1").Click();

        // Act
        cut.Find("#import-trip-add-participant-1").Click();

        // Assert
        cut.FindAll("[id^='import-trip-participant-']").Should().BeEmpty();
        await modal.Received(1).ShowAsync<AnglerPickerModal, AnglerPickerModalModel, AnglerPickerModalResult>(
            Arg.Any<AnglerPickerModalModel>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRemoveACatchFromASuggestedTrip()
    {
        // Arrange
        await using var context = CreateContext(Proposals(), Substitute.For<IImportPhotoPreparationService>());
        var cut = context.Render<ImportCatchCatalogue>();
        await ReachTripReviewAsync(cut, ReadyPhoto(0), ReadyPhoto(1));
        cut.FindAll("[id^='import-trip-catch-1-']").Should().HaveCount(2);

        // Act
        cut.Find("#import-trip-remove-1-1").Click();

        // Assert
        cut.FindAll("[id^='import-trip-catch-1-']").Should().HaveCount(1);
        cut.Find("#import-trip-catch-1-1").TextContent.Should().Contain("Catch 2");
    }

    [Fact]
    public async Task ItShouldOfferAndRecordAnExistingTripWhenOneAlreadyCoversTheCatches()
    {
        // Arrange
        var existingTripId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var existingTrips = Substitute.For<IImportExistingTripService>();
        existingTrips.GetCandidatesAsync(
                Arg.Any<IReadOnlyList<ImportTripProposalModel>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<IReadOnlyList<ImportTripProposalModel>>()
                .ToDictionary(
                    proposal => proposal.Id,
                    _ => (IReadOnlyList<TripSummaryDto>)
                    [
                        new TripSummaryDto(existingTripId, "Completed", CapturedOn) { Title = "Corrib" }
                    ]));
        await using var context = CreateContext(Proposals(), Substitute.For<IImportPhotoPreparationService>());
        context.Services.AddSingleton(existingTrips);
        var cut = context.Render<ImportCatchCatalogue>();
        await ReachTripReviewAsync(cut);

        // Assert
        cut.Find("#import-trip-existing-warning-1").Should().NotBeNull();

        // Act
        await cut.InvokeAsync(() => cut.FindComponent<TripSelector>().Instance.SelectedTripIdChanged
            .InvokeAsync(existingTripId));
        cut.Find("#import-trip-continue").Click();

        // Assert
        cut.Find("#import-summary-existing-trips").TextContent.Should().Contain("1");
        await existingTrips.Received(1).GetCandidatesAsync(
            Arg.Is<IReadOnlyList<ImportTripProposalModel>>(proposals => proposals.Count == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotImportWhileOffline()
    {
        // Arrange
        var network = Substitute.For<INetworkService>();
        network.IsOnlineAsync(Arg.Any<CancellationToken>()).Returns(false);
        var persistence = Substitute.For<IImportPersistenceService>();
        await using var context = CreateContext(
            Proposals(),
            Substitute.For<IImportPhotoPreparationService>(),
            persistenceService: persistence,
            networkService: network);
        var cut = context.Render<ImportCatchCatalogue>();
        await ReachConfirmationAsync(cut);

        // Act
        cut.Find("#import-confirm").Click();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#import-persistence-error").Should().NotBeNull());
        await network.Received(1).IsOnlineAsync(Arg.Any<CancellationToken>());
        await persistence.DidNotReceive().PersistAsync(
            Arg.Any<ImportBatchModel>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<IProgress<ImportPersistenceProgressModel>>());
    }

    [Theory]
    [InlineData(ImportPersistenceFailureEnum.Trip)]
    [InlineData(ImportPersistenceFailureEnum.Catch)]
    [InlineData(ImportPersistenceFailureEnum.Photograph)]
    [InlineData(ImportPersistenceFailureEnum.Verification)]
    public async Task ItShouldExplainWhichPartOfTheImportFailed(ImportPersistenceFailureEnum failure)
    {
        // Arrange
        var persistence = Substitute.For<IImportPersistenceService>();
        persistence.PersistAsync(
                Arg.Any<ImportBatchModel>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<IProgress<ImportPersistenceProgressModel>>())
            .Returns(ImportPersistenceResultModel.Failed(failure, new InvalidOperationException("detail")));
        var logging = Substitute.For<ILoggingService>();
        await using var context = CreateContext(
            Proposals(),
            Substitute.For<IImportPhotoPreparationService>(),
            persistenceService: persistence,
            loggingService: logging);
        var cut = context.Render<ImportCatchCatalogue>();
        await ReachConfirmationAsync(cut);

        // Act
        cut.Find("#import-confirm").Click();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#import-persistence-error").TextContent.Should().NotBeNullOrWhiteSpace());
        await persistence.Received(1).PersistAsync(
            Arg.Any<ImportBatchModel>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<IProgress<ImportPersistenceProgressModel>>());
        await logging.Received(1).LogErrorAsync(
            "persisting a historical Import",
            Arg.Is<string>(message => message.Contains(nameof(InvalidOperationException), StringComparison.Ordinal)
                && !message.Contains("detail", StringComparison.Ordinal)),
            CancellationToken.None);
    }

    [Fact]
    public async Task ItShouldShowTheGenericFailureAndLogWhenPersistenceThrows()
    {
        // Arrange
        var persistence = Substitute.For<IImportPersistenceService>();
        persistence.PersistAsync(
                Arg.Any<ImportBatchModel>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<IProgress<ImportPersistenceProgressModel>>())
            .Returns<Task<ImportPersistenceResultModel>>(_ => throw new InvalidOperationException("private detail"));
        var logging = Substitute.For<ILoggingService>();
        await using var context = CreateContext(
            Proposals(),
            Substitute.For<IImportPhotoPreparationService>(),
            persistenceService: persistence,
            loggingService: logging);
        var cut = context.Render<ImportCatchCatalogue>();
        await ReachConfirmationAsync(cut);

        // Act
        cut.Find("#import-confirm").Click();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#import-persistence-error").Should().NotBeNull());
        cut.Find("#import-persistence-error").TextContent.Should().NotContain("private detail");
        await logging.Received(1).LogErrorAsync(
            "persisting a historical Import",
            Arg.Is<string>(message => message.Contains(nameof(InvalidOperationException), StringComparison.Ordinal)
                && !message.Contains("private detail", StringComparison.Ordinal)),
            CancellationToken.None);
    }

    [Theory]
    [InlineData(ImportPersistenceStageEnum.SavingTrip)]
    [InlineData(ImportPersistenceStageEnum.SavingCatch)]
    [InlineData(ImportPersistenceStageEnum.UploadingPhotograph)]
    public async Task ItShouldShowTheCountsForEachCountedPersistenceStageWhileImporting(ImportPersistenceStageEnum stage)
    {
        // Arrange
        var persistence = Substitute.For<IImportPersistenceService>();
        var completion = new TaskCompletionSource<ImportPersistenceResultModel>();
        persistence.PersistAsync(
                Arg.Any<ImportBatchModel>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<IProgress<ImportPersistenceProgressModel>>())
            .Returns(call =>
            {
                call.ArgAt<IProgress<ImportPersistenceProgressModel>>(2)
                    .Report(new ImportPersistenceProgressModel(stage, 2, 5));
                return completion.Task;
            });
        await using var context = CreateContext(
            Proposals(),
            Substitute.For<IImportPhotoPreparationService>(),
            persistenceService: persistence);
        var cut = context.Render<ImportCatchCatalogue>();
        await ReachConfirmationAsync(cut);

        // Act
        cut.Find("#import-confirm").Click();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#import-persistence-progress").TextContent.Should().Contain("2").And.Contain("5"));
        completion.SetResult(new ImportPersistenceResultModel([], [Guid.NewGuid()], 1, 0));
    }

    [Fact]
    public async Task ItShouldShowAnUncountedMessageWhileVerifyingTheImport()
    {
        // Arrange
        var persistence = Substitute.For<IImportPersistenceService>();
        var completion = new TaskCompletionSource<ImportPersistenceResultModel>();
        persistence.PersistAsync(
                Arg.Any<ImportBatchModel>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<IProgress<ImportPersistenceProgressModel>>())
            .Returns(call =>
            {
                call.ArgAt<IProgress<ImportPersistenceProgressModel>>(2)
                    .Report(new ImportPersistenceProgressModel(ImportPersistenceStageEnum.Verifying, 1, 1));
                return completion.Task;
            });
        await using var context = CreateContext(
            Proposals(),
            Substitute.For<IImportPhotoPreparationService>(),
            persistenceService: persistence);
        var cut = context.Render<ImportCatchCatalogue>();
        await ReachConfirmationAsync(cut);

        // Act
        cut.Find("#import-confirm").Click();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#import-persistence-progress").TextContent
            .Should().Contain("Verifying"));
        completion.SetResult(new ImportPersistenceResultModel([], [Guid.NewGuid()], 1, 0));
    }

    [Fact]
    public async Task ItShouldNavigateToTheCatchesAndTripsAfterASuccessfulImport()
    {
        // Arrange
        await using var context = CreateContext(Proposals(), Substitute.For<IImportPhotoPreparationService>());
        var cut = context.Render<ImportCatchCatalogue>();
        await ReachConfirmationAsync(cut);
        cut.Find("#import-confirm").Click();
        cut.WaitForElement("#import-success");
        var navigation = context.Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();

        // Act
        cut.Find("#import-view-trips").Click();
        var tripsUri = navigation.Uri;
        cut.Find("#import-view-catches").Click();

        // Assert
        tripsUri.Should().EndWith("/trips");
        navigation.Uri.Should().EndWith("/catches");
    }
}
