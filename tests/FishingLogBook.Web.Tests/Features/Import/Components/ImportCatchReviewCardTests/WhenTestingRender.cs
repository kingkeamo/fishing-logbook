using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Import.Components.ImportCatchReviewCard;
using FishingLogBook.Web.Features.Import.Enums;
using FishingLogBook.Web.Features.Import.Models;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

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

        // Act
        var cut = context.Render<ImportCatchReviewCard>(parameters => parameters
            .Add(component => component.Proposal, proposal)
            .Add(component => component.Photos, [photo])
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

        // Act
        var cut = context.Render<ImportCatchReviewCard>(parameters => parameters
            .Add(component => component.Proposal,
                Proposal(photo, timestamp, ImportCatchProposalReasonEnum.TrustworthyCaptureTime))
            .Add(component => component.Photos, [photo])
            .Add(component => component.Number, 1));

        // Assert
        cut.Find("#import-catch-1-timestamp").TextContent.Should().Contain("+01:00");
        cut.Find("#import-catch-1-status").TextContent.Should().Contain("Ready");
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

        // Act
        var cut = context.Render<ImportCatchReviewCard>(parameters => parameters
            .Add(component => component.Proposal, proposal)
            .Add(component => component.Photos, [photo])
            .Add(component => component.Number, 1));

        // Assert
        cut.Find("#import-catch-1-location").TextContent.Should().Contain("review required");
        cut.Find("#import-catch-1-status").TextContent.Should().Contain("Needs review");
        cut.Markup.Should().NotContain("53.3498");
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
            photo.Location,
            reasons);
    }
}
