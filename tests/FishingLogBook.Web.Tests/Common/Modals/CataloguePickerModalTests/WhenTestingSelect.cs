using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Localization;

namespace FishingLogBook.Web.Tests.Common.Modals.CataloguePickerModalTests;

public class WhenTestingSelect : BaseCataloguePickerModalTest
{
    [Fact]
    public async Task ItShouldShowTheEmptyMessageWhenThereIsNothingToChooseFrom()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();

        // Act
        var (cut, _) = await ShowAsync(context, new CataloguePickerModalModel("Species", []));

        // Assert
        cut.Find("#catalogue-picker-modal-empty").TextContent.Should().Contain("No matches found.");
        cut.FindAll("#catalogue-picker-modal-options").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldCancelWithoutChoosingAnOption()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var (cut, dialog) = await ShowAsync(context, DefaultModel());

        // Act
        await cut.Find("#catalogue-picker-modal-cancel").ClickAsync();
        var result = await dialog.Result;

        // Assert
        result!.Canceled.Should().BeTrue();
        result.Data.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldFilterTheOptionsByTheSearchTerm()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var (cut, _) = await ShowAsync(context, DefaultModel());

        // Act
        cut.Find("#catalogue-picker-modal-search").Input("tro");

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#catalogue-picker-modal-option-BrownTrout").Should().NotBeNull();
            cut.FindAll("#catalogue-picker-modal-option-Pike").Should().BeEmpty();
            cut.FindAll("#catalogue-picker-modal-option-Tench").Should().BeEmpty();
        });
    }

    [Fact]
    public async Task ItShouldShowTheEmptyMessageWhenTheSearchMatchesNothing()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var (cut, _) = await ShowAsync(context, DefaultModel());

        // Act
        cut.Find("#catalogue-picker-modal-search").Input("marlin");

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catalogue-picker-modal-empty").TextContent.Should().Contain("No matches found."));
    }

    [Fact]
    public async Task ItShouldShowFrenchCopy()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        await using var context = CreateContext();

        // Act
        var (cut, _) = await ShowAsync(context, new CataloguePickerModalModel("Espèces", []));

        // Assert
        cut.Find("#catalogue-picker-modal-empty").TextContent.Should().Contain("Aucun résultat.");
        cut.Find("#catalogue-picker-modal-cancel").TextContent.Should().Contain("Annuler");
    }

    [Fact]
    public async Task ItShouldCloseWithTheChosenOption()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var (cut, dialog) = await ShowAsync(context, DefaultModel());
        cut.Find("#catalogue-picker-modal-title").TextContent.Should().Contain("Species");

        // Act
        await cut.Find("#catalogue-picker-modal-option-Pike").ClickAsync();
        var result = await dialog.Result;

        // Assert
        result!.Canceled.Should().BeFalse();
        result.Data.Should().BeOfType<CataloguePickerModalResult>();
        ((CataloguePickerModalResult)result.Data!).Option.Id.Should().Be(PikeSpeciesId);
        ((CataloguePickerModalResult)result.Data!).Option.Name.Should().Be("Pike");
    }
}
