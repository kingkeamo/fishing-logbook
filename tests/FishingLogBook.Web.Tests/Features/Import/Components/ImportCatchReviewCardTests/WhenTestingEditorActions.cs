using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Import.Models;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Import.Components.ImportCatchReviewCardTests;

public class WhenTestingEditorActions : BaseImportCatchReviewCardTest
{
    private static readonly Guid FirstCatchId = Guid.Parse("22222222-2222-2222-2222-222222222221");
    private static readonly Guid SecondCatchId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task ItShouldChangeTheMethodAndSpeciesFromTheShortlistChips()
    {
        // Arrange
        var photo = Photo();
        var proposal = Proposal(FirstCatchId, photo);
        var batch = Batch([photo], proposal);
        await using var context = CreateContext();
        var cut = Render(context, batch, proposal, Preferences());
        await cut.Find("#import-catch-1-edit").ClickAsync();

        // Act
        await cut.Find("#import-catch-1-method-Spinning").ClickAsync();
        await cut.Find("#import-catch-1-species-Pike").ClickAsync();

        // Assert
        proposal.Method.Id.Should().Be(SpinningId);
        proposal.Species.Id.Should().Be(PikeId);
    }

    [Fact]
    public async Task ItShouldApplyTheMethodAndSpeciesChosenInTheFullCatalogue()
    {
        // Arrange
        var photo = Photo();
        var proposal = Proposal(FirstCatchId, photo);
        var batch = Batch([photo], proposal);
        var modal = Substitute.For<IModalService>();
        modal.ShowAsync<CataloguePickerModal, CataloguePickerModalModel, CataloguePickerModalResult>(
                Arg.Any<CataloguePickerModalModel>(),
                Arg.Any<CancellationToken>())
            .Returns(
                new CataloguePickerModalResult([new CatalogueOptionModel(SpinningId, "Spinning", "Spinning")]),
                new CataloguePickerModalResult([new CatalogueOptionModel(PikeId, "Pike", "Pike")]));
        await using var context = CreateContext(modal);
        var cut = Render(context, batch, proposal, Preferences());
        await cut.Find("#import-catch-1-edit").ClickAsync();

        // Act
        await cut.Find("#import-catch-1-method-more").ClickAsync();
        await cut.Find("#import-catch-1-species-more").ClickAsync();

        // Assert
        proposal.Method.Id.Should().Be(SpinningId);
        proposal.Species.Id.Should().Be(PikeId);
        await modal.Received(2).ShowAsync<CataloguePickerModal, CataloguePickerModalModel, CataloguePickerModalResult>(
            Arg.Any<CataloguePickerModalModel>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepTheMethodAndSpeciesWhenTheCataloguePickerIsDismissed()
    {
        // Arrange
        var photo = Photo();
        var proposal = Proposal(FirstCatchId, photo);
        var batch = Batch([photo], proposal);
        var modal = Substitute.For<IModalService>();
        modal.ShowAsync<CataloguePickerModal, CataloguePickerModalModel, CataloguePickerModalResult>(
                Arg.Any<CataloguePickerModalModel>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CataloguePickerModalResult?>(null));
        await using var context = CreateContext(modal);
        var cut = Render(context, batch, proposal, Preferences());
        await cut.Find("#import-catch-1-edit").ClickAsync();

        // Act
        await cut.Find("#import-catch-1-method-more").ClickAsync();
        await cut.Find("#import-catch-1-species-more").ClickAsync();

        // Assert
        proposal.Method.Id.Should().Be(FlyId);
        proposal.Species.Id.Should().Be(BrownTroutId);
        await modal.Received(2).ShowAsync<CataloguePickerModal, CataloguePickerModalModel, CataloguePickerModalResult>(
            Arg.Any<CataloguePickerModalModel>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldMergeAnotherCatchIntoThisOneAndReportTheChange()
    {
        // Arrange
        var firstPhoto = Photo(0);
        var secondPhoto = Photo(1);
        var first = Proposal(FirstCatchId, firstPhoto);
        var second = Proposal(SecondCatchId, secondPhoto);
        var batch = Batch([firstPhoto, secondPhoto], first, second);
        var changes = 0;
        await using var context = CreateContext();
        var cut = Render(context, batch, first, Preferences(), changed: () => changes++);
        await cut.Find("#import-catch-1-edit").ClickAsync();

        // Act
        await cut.Find("#import-catch-1-merge-2").ClickAsync();

        // Assert
        changes.Should().Be(1);
        first.PhotoIds.Should().BeEquivalentTo([firstPhoto.Id, secondPhoto.Id]);
        second.IsRemoved.Should().BeTrue();
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public async Task ItShouldRemoveTheCatchOnlyWhenTheAnglerConfirms(bool confirmed, int expectedRemovals)
    {
        // Arrange
        var photo = Photo();
        var proposal = Proposal(FirstCatchId, photo);
        var batch = Batch([photo], proposal);
        var modal = Substitute.For<IModalService>();
        modal.ConfirmAsync(Arg.Any<ConfirmModalModel>(), Arg.Any<CancellationToken>()).Returns(confirmed);
        var removed = new List<Guid>();
        await using var context = CreateContext(modal);
        var cut = Render(context, batch, proposal, Preferences(), removeCatch: removed.Add);
        await cut.Find("#import-catch-1-edit").ClickAsync();

        // Act
        await cut.Find("#import-catch-1-remove").ClickAsync();

        // Assert
        removed.Should().HaveCount(expectedRemovals);
        if (confirmed)
        {
            removed.Should().ContainSingle().Which.Should().Be(FirstCatchId);
        }

        await modal.Received(1).ConfirmAsync(Arg.Any<ConfirmModalModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldCloseTheEditorWhenBackIsChosen()
    {
        // Arrange
        var photo = Photo();
        var proposal = Proposal(FirstCatchId, photo);
        var batch = Batch([photo], proposal);
        await using var context = CreateContext();
        var cut = Render(context, batch, proposal, Preferences());
        await cut.Find("#import-catch-1-edit").ClickAsync();
        cut.Find("#import-catch-1-editor").Should().NotBeNull();

        // Act
        await cut.Find("#import-catch-1-close-editor").ClickAsync();

        // Assert
        cut.FindAll("#import-catch-1-editor").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldShowWeightAndLengthInTheAnglersImperialUnits()
    {
        // Arrange
        var photo = Photo();
        var proposal = Proposal(FirstCatchId, photo);
        var batch = Batch([photo], proposal);
        batch.SetCatchWeight(FirstCatchId, 2.5m);
        batch.SetCatchLength(FirstCatchId, 50m);
        await using var context = CreateContext();

        // Act
        var cut = Render(
            context,
            batch,
            proposal,
            Preferences(WeightUnitEnum.Lb, LengthUnitEnum.In),
            editable: false);

        // Assert
        var label = cut.Find("#import-catch-1-measurements").TextContent;
        label.Should().Contain("lb").And.Contain("in");
    }

    [Fact]
    public async Task ItShouldShowNoMeasurementsLineWhenNeitherWeightNorLengthIsKnown()
    {
        // Arrange
        var photo = Photo();
        var proposal = Proposal(FirstCatchId, photo);
        var batch = Batch([photo], proposal);
        await using var context = CreateContext();

        // Act
        var cut = Render(context, batch, proposal, Preferences(), editable: false);

        // Assert
        cut.FindAll("#import-catch-1-measurements").Should().BeEmpty();
    }
}
