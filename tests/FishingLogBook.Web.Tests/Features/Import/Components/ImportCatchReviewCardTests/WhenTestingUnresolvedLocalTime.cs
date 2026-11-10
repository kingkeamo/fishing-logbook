using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Features.Import.Enums;
using FishingLogBook.Web.Features.Import.Models;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Import.Components.ImportCatchReviewCardTests;

public class WhenTestingUnresolvedLocalTime : BaseImportCatchReviewCardTest
{
    private static readonly Guid CatchId = Guid.Parse("22222222-2222-2222-2222-222222222221");
    private static readonly DateTime WallClock = new(2025, 6, 14, 9, 30, 0);
    private const string WallClockValue = "2025-06-14T09:30";

    private static (ImportBatchModel Batch, ImportCatchProposalModel Proposal) WallClockCatch()
    {
        var photo = Photo();
        var proposal = new ImportCatchProposalModel(
            CatchId,
            [photo.Id],
            ImportTimestampModel.FromLocalWallClock(WallClock, ImportTimestampSourceEnum.ExifOriginal),
            new ImportCatalogueSelectionModel(FlyId, "Fly", "Fly"),
            new ImportCatalogueSelectionModel(BrownTroutId, "BrownTrout", "Brown Trout"),
            photo.Location.Accept(),
            [ImportCatchProposalReasonEnum.TrustworthyCaptureTime]);
        return (Batch([photo], proposal), proposal);
    }

    private static ITimeService UnresolvableTime(BunitContext context)
    {
        var time = context.Services.GetRequiredService<ITimeService>();
        time.FromDateTimeLocalValueAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((DateTimeOffset?)null);
        return time;
    }

    [Fact]
    public async Task ItShouldNotConfirmTheCatchWhenTheBrowserCannotResolveItsLocalTime()
    {
        // Arrange
        var (batch, proposal) = WallClockCatch();
        await using var context = CreateContext();
        var time = UnresolvableTime(context);
        var changes = 0;
        var cut = Render(context, batch, proposal, Preferences(), changed: () => changes++, editable: false);

        // Act
        await cut.Find("#import-catch-1-confirm").ClickAsync();

        // Assert
        proposal.ReviewStatus.Should().NotBe(ImportCatchReviewStatusEnum.Reviewed);
        proposal.CaughtOn.State.Should().Be(ImportTimestampStateEnum.LocalWallClock);
        changes.Should().Be(0);
        await time.Received(1).FromDateTimeLocalValueAsync(WallClockValue, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepTheEditorOpenWhenConfirmingTheCaughtOnTimeCannotBeResolved()
    {
        // Arrange
        var (batch, proposal) = WallClockCatch();
        await using var context = CreateContext();
        var time = UnresolvableTime(context);
        var cut = Render(context, batch, proposal, Preferences());
        await cut.Find("#import-catch-1-edit").ClickAsync();

        // Act
        await cut.Find("#import-catch-1-confirm-caught-on").ClickAsync();

        // Assert
        cut.Find("#import-catch-1-editor").Should().NotBeNull();
        proposal.CaughtOn.State.Should().Be(ImportTimestampStateEnum.LocalWallClock);
        await time.Received(1).FromDateTimeLocalValueAsync(WallClockValue, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepTheEditorOpenWhenContinuingWithAnUnresolvableCaughtOnTime()
    {
        // Arrange
        var (batch, proposal) = WallClockCatch();
        await using var context = CreateContext();
        var time = UnresolvableTime(context);
        var changes = 0;
        var cut = Render(context, batch, proposal, Preferences(), changed: () => changes++);
        await cut.Find("#import-catch-1-edit").ClickAsync();

        // Act
        await cut.Find("#import-catch-1-continue").ClickAsync();

        // Assert
        cut.Find("#import-catch-1-editor").Should().NotBeNull();
        proposal.ReviewStatus.Should().NotBe(ImportCatchReviewStatusEnum.Reviewed);
        changes.Should().Be(0);
        await time.Received(1).FromDateTimeLocalValueAsync(WallClockValue, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowOnlyTheWeightWhenNoLengthIsKnown()
    {
        // Arrange
        var photo = Photo();
        var proposal = Proposal(CatchId, photo);
        var batch = Batch([photo], proposal);
        batch.SetCatchWeight(CatchId, 2.5m);
        await using var context = CreateContext();

        // Act
        var cut = Render(context, batch, proposal, Preferences(), editable: false);

        // Assert
        var label = cut.Find("#import-catch-1-measurements").TextContent;
        label.Should().Contain("2.5").And.Contain("kg").And.NotContain("cm");
    }

    [Fact]
    public async Task ItShouldShowOnlyTheLengthWhenNoWeightIsKnown()
    {
        // Arrange
        var photo = Photo();
        var proposal = Proposal(CatchId, photo);
        var batch = Batch([photo], proposal);
        batch.SetCatchLength(CatchId, 50m);
        await using var context = CreateContext();

        // Act
        var cut = Render(context, batch, proposal, Preferences(), editable: false);

        // Assert
        var label = cut.Find("#import-catch-1-measurements").TextContent;
        label.Should().Contain("50").And.Contain("cm").And.NotContain("kg");
    }
}
