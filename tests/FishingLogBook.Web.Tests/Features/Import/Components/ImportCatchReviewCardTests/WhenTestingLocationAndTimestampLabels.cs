using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Import.Enums;
using FishingLogBook.Web.Features.Import.Models;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace FishingLogBook.Web.Tests.Features.Import.Components.ImportCatchReviewCardTests;

public class WhenTestingLocationAndTimestampLabels : BaseImportCatchReviewCardTest
{
    private static readonly Guid CatchProposalId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static ImportCatchProposalModel ProposalWith(
        ImportSelectedPhotoModel photo,
        ImportLocationModel? location,
        ImportTimestampModel? timestamp = null)
    {
        return new ImportCatchProposalModel(
            CatchProposalId,
            [photo.Id],
            timestamp ?? photo.Timestamp,
            new ImportCatalogueSelectionModel(FlyId, "Fly", "Fly"),
            new ImportCatalogueSelectionModel(BrownTroutId, "BrownTrout", "Brown Trout"),
            location,
            [ImportCatchProposalReasonEnum.TrustworthyCaptureTime]);
    }

    private static string Text(BunitContext context, string key)
    {
        return context.Services.GetRequiredService<IStringLocalizer<UiStrings>>()[key].Value;
    }

    private async Task<string> LocationLabelAsync(ImportLocationModel? location, string expectedKey)
    {
        using var culture = TestCulture.Use(CultureNames.English);
        var photo = Photo();
        var proposal = ProposalWith(photo, location);
        var batch = Batch([photo], proposal);
        await using var context = CreateContext();
        var cut = Render(context, batch, proposal, Preferences(), editable: false);
        var label = cut.Find("#import-catch-1-location").TextContent;
        label.Should().Contain(Text(context, expectedKey));
        return label;
    }

    [Fact]
    public async Task ItShouldSayTheLocationWasRemoved()
    {
        // Arrange
        // Act
        var label = await LocationLabelAsync(
            new ImportLocationModel(null, null, false, ImportLocationDecisionEnum.Removed),
            "Import_LocationRemoved");

        // Assert
        label.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ItShouldShowAnAcceptedLocationWithoutAPlaceNameAsAccepted()
    {
        // Arrange
        // Act
        var label = await LocationLabelAsync(
            new ImportLocationModel(53.3498, -6.2603, true).Accept(),
            "Import_LocationAccepted");

        // Assert
        label.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ItShouldOfferALocationWithCoordinatesThatHasNotBeenAcceptedYetAsAvailable()
    {
        // Arrange
        // Act
        var label = await LocationLabelAsync(
            new ImportLocationModel(53.3498, -6.2603, true),
            "Import_LocationAvailable");

        // Assert
        label.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ItShouldSayTheLocationIsUnavailableWhenThereIsNone()
    {
        // Arrange
        // Act
        var label = await LocationLabelAsync(null, "Import_LocationUnavailable");

        // Assert
        label.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ItShouldShowThePlaceNameOfAnAcceptedLocation()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var photo = Photo();
        var location = new ImportLocationModel(53.3498, -6.2603, true).WithPlaceName("Lough Corrib").Accept();
        var proposal = ProposalWith(photo, location);
        var batch = Batch([photo], proposal);
        await using var context = CreateContext();

        // Act
        var cut = Render(context, batch, proposal, Preferences(), editable: false);

        // Assert
        cut.Find("#import-catch-1-location").TextContent.Should().Contain("Lough Corrib");
    }

    [Fact]
    public async Task ItShouldShowAUserConfirmedTimeWithoutAnInstantFromItsWallClock()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var photo = Photo();
        var wallClock = new DateTime(2009, 2, 2, 15, 6, 0);
        var timestamp = ImportTimestampModel.FromLocalWallClock(wallClock, ImportTimestampSourceEnum.ExifOriginal)
            .Confirm(wallClock);
        var proposal = ProposalWith(photo, photo.Location.Accept(), timestamp);
        var batch = Batch([photo], proposal);
        await using var context = CreateContext();

        // Act
        var cut = Render(context, batch, proposal, Preferences(), editable: false);

        // Assert
        cut.Find("#import-catch-1-timestamp").TextContent.Should().Contain("02 Feb 2009");
    }
}
