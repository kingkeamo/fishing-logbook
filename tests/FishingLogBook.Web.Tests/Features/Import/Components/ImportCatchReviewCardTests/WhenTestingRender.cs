using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Catch.Components.MeasurementEditor;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Import.Components.ImportCatchReviewCard;
using FishingLogBook.Web.Features.Import.Enums;
using FishingLogBook.Web.Features.Import.Models;
using FishingLogBook.Web.Features.Profile.Models;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Import.Components.ImportCatchReviewCardTests;

public class WhenTestingRender
{
    private static readonly DateTimeOffset CapturedOn = DateTimeOffset.Parse("2025-06-14T09:30:00+01:00");

    [Theory]
    [MemberData(nameof(ReviewTimestamps))]
    public async Task ItShouldPresentUnresolvedTimestampsAsNeedingReview(
        ImportTimestampModel timestamp,
        ImportCatchProposalReasonEnum reason,
        string expected)
    {
        // Arrange
        await using var context = CreateContext();
        var photo = Photo(timestamp);
        var proposal = Proposal(photo, timestamp, reason);
        var batch = Batch(photo, proposal);

        // Act
        var cut = context.Render<ImportCatchReviewCard>(parameters => parameters
            .Add(component => component.Proposal, proposal)
            .Add(component => component.Batch, batch)
            .Add(component => component.Preferences, Preferences())
            .Add(component => component.Number, 1));

        // Assert
        cut.Find("#import-catch-1-timestamp").TextContent.Should().Contain(expected);
        cut.Find("#import-catch-1-status").TextContent.Should().Contain("Needs review");
    }

    [Fact]
    public async Task ItShouldPreserveTheHistoricalOffsetForATrustworthyTimestamp()
    {
        // Arrange
        await using var context = CreateContext();
        var timestamp = ImportTimestampModel.FromExplicitInstant(CapturedOn, ImportTimestampSourceEnum.ExifOriginal);
        var photo = Photo(timestamp);
        var proposal = Proposal(photo, timestamp, ImportCatchProposalReasonEnum.TrustworthyCaptureTime);
        var batch = Batch(photo, proposal);

        // Act
        var cut = context.Render<ImportCatchReviewCard>(parameters => parameters
            .Add(component => component.Proposal, proposal)
            .Add(component => component.Batch, batch)
            .Add(component => component.Preferences, Preferences())
            .Add(component => component.Number, 1));

        // Assert
        cut.Find("#import-catch-1-timestamp").TextContent.Should().Contain("+01:00");
        cut.Find("#import-catch-1-status").TextContent.Should().Contain("Ready");
    }

    [Fact]
    public async Task ItShouldOfferReviewAndConfirmTogetherForDisplayedSuggestedValues()
    {
        // Arrange
        await using var context = CreateContext();
        var timestamp = ImportTimestampModel.FromWeakFallback(CapturedOn);
        var photo = Photo(timestamp);
        var proposal = Proposal(photo, timestamp, ImportCatchProposalReasonEnum.WeakTimestamp);
        var cut = context.Render<ImportCatchReviewCard>(parameters => parameters
            .Add(component => component.Proposal, proposal)
            .Add(component => component.Batch, Batch(photo, proposal))
            .Add(component => component.Preferences, Preferences())
            .Add(component => component.Number, 1)
            .Add(component => component.Editable, true));

        // Act
        var review = cut.Find("#import-catch-1-edit");
        cut.Find("#import-catch-1-confirm").Click();

        // Assert
        review.TextContent.Should().Contain("Review");
        proposal.CaughtOn.State.Should().Be(ImportTimestampStateEnum.UserConfirmed);
        proposal.ReviewStatus.Should().Be(ImportCatchReviewStatusEnum.Reviewed);
        cut.Find("#import-catch-1-status").TextContent.Should().Contain("Reviewed");
    }

    [Fact]
    public async Task ItShouldPresentConflictingGpsWithoutExposingCoordinates()
    {
        // Arrange
        await using var context = CreateContext();
        var timestamp = ImportTimestampModel.FromExplicitInstant(CapturedOn, ImportTimestampSourceEnum.ExifOriginal);
        var photo = Photo(timestamp);
        var proposal = Proposal(
            photo,
            timestamp,
            ImportCatchProposalReasonEnum.TrustworthyCaptureTime,
            ImportCatchProposalReasonEnum.ConflictingGps);
        var batch = Batch(photo, proposal);

        // Act
        var cut = context.Render<ImportCatchReviewCard>(parameters => parameters
            .Add(component => component.Proposal, proposal)
            .Add(component => component.Batch, batch)
            .Add(component => component.Preferences, Preferences())
            .Add(component => component.Number, 1));

        // Assert
        cut.Find("#import-catch-1-location").TextContent.Should().Contain("review required");
        cut.Find("#import-catch-1-status").TextContent.Should().Contain("Needs review");
        cut.Markup.Should().NotContain("53.3498");
    }

    [Fact]
    public async Task ItShouldConfirmAMissingHistoricalWallClockWithoutAddingAnOffset()
    {
        // Arrange
        await using var context = CreateContext();
        var timestamp = ImportTimestampModel.Missing();
        var photo = Photo(timestamp);
        var proposal = new ImportCatchProposalModel(
            Guid.NewGuid(), [photo.Id], timestamp,
            new ImportCatalogueSelectionModel(Guid.NewGuid(), "Fly", "Fly"),
            new ImportCatalogueSelectionModel(Guid.NewGuid(), "BrownTrout", "Brown Trout"));
        var batch = Batch(photo, proposal);
        var cut = context.Render<ImportCatchReviewCard>(parameters => parameters
            .Add(component => component.Proposal, proposal)
            .Add(component => component.Batch, batch)
            .Add(component => component.Preferences, Preferences())
            .Add(component => component.Number, 1)
            .Add(component => component.Editable, true));
        cut.Find("#import-catch-1-edit").Click();

        // Act
        cut.Find("#import-catch-1-caught-on").Input("2024-06-14T09:20");
        cut.Find("#import-catch-1-confirm-caught-on").Click();
        cut.Find("#import-catch-1-close-editor").Click();

        // Assert
        proposal.CaughtOn.Instant.Should().BeNull();
        proposal.CaughtOn.LocalWallClock.Should().Be(new DateTime(2024, 6, 14, 9, 20, 0, DateTimeKind.Unspecified));
        cut.Find("#import-catch-1-status").TextContent.Should().Contain("Ready");
    }

    [Fact]
    public async Task ItShouldAcceptTheLocalisedValueReturnedByTheDateTimePicker()
    {
        // Arrange
        await using var context = CreateContext();
        var timestamp = ImportTimestampModel.FromLocalWallClock(
            new DateTime(2009, 2, 2, 15, 6, 0),
            ImportTimestampSourceEnum.ExifOriginal);
        var photo = Photo(timestamp);
        var proposal = Proposal(photo, timestamp, ImportCatchProposalReasonEnum.AmbiguousTimestamp);
        var cut = context.Render<ImportCatchReviewCard>(parameters => parameters
            .Add(component => component.Proposal, proposal)
            .Add(component => component.Batch, Batch(photo, proposal))
            .Add(component => component.Preferences, Preferences())
            .Add(component => component.Number, 1)
            .Add(component => component.Editable, true));
        cut.Find("#import-catch-1-edit").Click();

        // Act
        cut.Find("#import-catch-1-caught-on").Input("09/04/2026 03:06 PM");
        cut.Find("#import-catch-1-confirm-caught-on").Click();

        // Assert
        proposal.CaughtOn.LocalWallClock.Should().Be(new DateTime(2026, 4, 9, 15, 6, 0));
        cut.FindAll(".mud-input-error").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldContinueFromTheEditorWithoutRequiringBack()
    {
        // Arrange
        await using var context = CreateContext();
        var timestamp = ImportTimestampModel.FromLocalWallClock(
            new DateTime(2009, 2, 2, 15, 6, 0),
            ImportTimestampSourceEnum.ExifOriginal);
        var photo = Photo(timestamp);
        var proposal = Proposal(photo, timestamp, ImportCatchProposalReasonEnum.AmbiguousTimestamp);
        var cut = context.Render<ImportCatchReviewCard>(parameters => parameters
            .Add(component => component.Proposal, proposal)
            .Add(component => component.Batch, Batch(photo, proposal))
            .Add(component => component.Preferences, Preferences())
            .Add(component => component.Number, 1)
            .Add(component => component.Editable, true));
        cut.Find("#import-catch-1-edit").Click();

        // Act
        cut.Find("#import-catch-1-caught-on").Input("09/04/2026 03:06 PM");
        cut.Find("#import-catch-1-continue").Click();

        // Assert
        proposal.ReviewStatus.Should().Be(ImportCatchReviewStatusEnum.Reviewed);
        cut.FindAll("#import-catch-1-editor").Should().BeEmpty();
        cut.Find("#import-catch-1-status").TextContent.Should().Contain("Reviewed");
    }

    [Fact]
    public async Task ItShouldImmediatelyRequireReviewWhenAReviewedCaughtOnValueBecomesInvalid()
    {
        // Arrange
        await using var context = CreateContext();
        var timestamp = ImportTimestampModel.FromExplicitInstant(CapturedOn, ImportTimestampSourceEnum.ExifOriginal);
        var photo = Photo(timestamp);
        var proposal = Proposal(photo, timestamp, ImportCatchProposalReasonEnum.TrustworthyCaptureTime);
        var batch = Batch(photo, proposal);
        proposal.MarkReviewed();
        var cut = context.Render<ImportCatchReviewCard>(parameters => parameters
            .Add(component => component.Proposal, proposal)
            .Add(component => component.Batch, batch)
            .Add(component => component.Preferences, Preferences())
            .Add(component => component.Number, 1)
            .Add(component => component.Editable, true));
        cut.Find("#import-catch-1-edit").Click();

        // Act
        cut.Find("#import-catch-1-caught-on").Input(string.Empty);

        // Assert
        proposal.ReviewStatus.Should().Be(ImportCatchReviewStatusEnum.Draft);
        cut.Find("#import-catch-1-status").TextContent.Should().Contain("Needs review");
        batch.CanAdvanceToTrips.Should().BeFalse();

        // Act
        cut.Find("#import-catch-1-continue").Click();

        // Assert
        proposal.ReviewStatus.Should().Be(ImportCatchReviewStatusEnum.Draft);
        cut.Find("#import-catch-1-status").TextContent.Should().Contain("Needs review");
        batch.CanAdvanceToTrips.Should().BeFalse();

        // Act
        cut.Find("#import-catch-1-caught-on").Input("2024-06-14T09:20");
        cut.Find("#import-catch-1-continue").Click();

        // Assert
        proposal.ReviewStatus.Should().Be(ImportCatchReviewStatusEnum.Reviewed);
        batch.CanAdvanceToTrips.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldResolveConflictingGpsOnlyAfterAnExplicitLocationChoice()
    {
        // Arrange
        await using var context = CreateContext();
        var timestamp = ImportTimestampModel.FromExplicitInstant(CapturedOn, ImportTimestampSourceEnum.ExifOriginal);
        var photo = Photo(timestamp);
        var proposal = Proposal(photo, timestamp,
            ImportCatchProposalReasonEnum.TrustworthyCaptureTime,
            ImportCatchProposalReasonEnum.ConflictingGps);
        var batch = Batch(photo, proposal);
        var cut = context.Render<ImportCatchReviewCard>(parameters => parameters
            .Add(component => component.Proposal, proposal)
            .Add(component => component.Batch, batch)
            .Add(component => component.Preferences, Preferences())
            .Add(component => component.Number, 1)
            .Add(component => component.Editable, true));
        cut.Find("#import-catch-1-edit").Click();

        // Act
        cut.Find("#import-catch-1-location-0").Click();

        // Assert
        proposal.HasUnresolvedGpsConflict.Should().BeFalse();
        proposal.Location!.Decision.Should().Be(ImportLocationDecisionEnum.Accepted);
        cut.Find("#import-catch-1-location").TextContent.Should().Contain("accepted");
    }

    [Fact]
    public async Task ItShouldReviewWeightAndLengthWithTheSharedMeasurementEditors()
    {
        // Arrange
        await using var context = CreateContext();
        var modal = context.Services.GetRequiredService<IModalService>();
        modal.ShowAsync<MeasurementEditorModal, MeasurementEditorModel, MeasurementEditorResult>(
                Arg.Is<MeasurementEditorModel>(model => model.IsWeight),
                Arg.Any<CancellationToken>())
            .Returns(new MeasurementEditorResult(2.5m));
        modal.ShowAsync<MeasurementEditorModal, MeasurementEditorModel, MeasurementEditorResult>(
                Arg.Is<MeasurementEditorModel>(model => !model.IsWeight),
                Arg.Any<CancellationToken>())
            .Returns(new MeasurementEditorResult(48m));
        var timestamp = ImportTimestampModel.FromExplicitInstant(CapturedOn, ImportTimestampSourceEnum.ExifOriginal);
        var photo = Photo(timestamp);
        var proposal = Proposal(photo, timestamp, ImportCatchProposalReasonEnum.TrustworthyCaptureTime);
        var cut = context.Render<ImportCatchReviewCard>(parameters => parameters
            .Add(component => component.Proposal, proposal)
            .Add(component => component.Batch, Batch(photo, proposal))
            .Add(component => component.Preferences, Preferences())
            .Add(component => component.Number, 1)
            .Add(component => component.Editable, true));

        // Act
        cut.Find("#import-catch-1-edit").Click();
        cut.Find("#import-catch-1-weight").Click();
        cut.Find("#import-catch-1-length").Click();

        // Assert
        proposal.Weight.Should().Be(2.5m);
        proposal.Length.Should().Be(48m);
    }

    public static TheoryData<ImportTimestampModel, ImportCatchProposalReasonEnum, string> ReviewTimestamps => new()
    {
        { ImportTimestampModel.Missing(), ImportCatchProposalReasonEnum.MissingTimestamp, "Date and time required" },
        { ImportTimestampModel.Unusable(ImportTimestampSourceEnum.ExifOriginal), ImportCatchProposalReasonEnum.UnusableTimestamp, "could not be used" },
        { ImportTimestampModel.FromWeakFallback(CapturedOn), ImportCatchProposalReasonEnum.WeakTimestamp, "suggested date requires confirmation" },
        {
            ImportTimestampModel.FromLocalWallClock(
                new DateTime(2025, 6, 14, 9, 30, 0),
                ImportTimestampSourceEnum.ExifOriginal),
            ImportCatchProposalReasonEnum.AmbiguousTimestamp,
            "timezone confirmation required"
        }
    };

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton<IMeasurementService, MeasurementService>();
        context.Services.AddSingleton(Substitute.For<IModalService>());
        return context;
    }

    private static ImportSelectedPhotoModel Photo(ImportTimestampModel timestamp)
    {
        var photo = new ImportSelectedPhotoModel(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            0,
            "image/jpeg",
            1024,
            "token",
            "catch.jpg",
            "blob:thumbnail");
        photo.SetPreparation(ImportPhotoPreparationStatusEnum.Ready, "token", "blob:thumbnail");
        photo.SetMetadata(
            ImportMetadataStatusEnum.Available,
            timestamp,
            new ImportLocationModel(53.3498, -6.2603, true));
        return photo;
    }

    private static ImportCatchProposalModel Proposal(
        ImportSelectedPhotoModel photo,
        ImportTimestampModel timestamp,
        params ImportCatchProposalReasonEnum[] reasons)
    {
        return new ImportCatchProposalModel(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            [photo.Id],
            timestamp,
            new ImportCatalogueSelectionModel(Guid.NewGuid(), "Fly", "Fly"),
            new ImportCatalogueSelectionModel(Guid.NewGuid(), "BrownTrout", "Brown Trout"),
            reasons.Contains(ImportCatchProposalReasonEnum.ConflictingGps) ? photo.Location : photo.Location.Accept(),
            reasons);
    }

    private static ImportBatchModel Batch(ImportSelectedPhotoModel photo, ImportCatchProposalModel proposal)
    {
        var batch = new ImportBatchModel(Guid.NewGuid(), proposal.Method, proposal.Species);
        batch.AddPhoto(photo);
        batch.AddCatchProposal(proposal);
        return batch;
    }

    private static AnglerPreferencesModel Preferences()
    {
        return new AnglerPreferencesModel(
            new FishingCatalogueDto([], []),
            new FishingPreferencesDto([]),
            WeightUnitEnum.Kg,
            LengthUnitEnum.Cm);
    }
}

