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
        cut.Find("#catalogue-picker-modal-selected-label").TextContent.Should().Contain("Selected");
        cut.Find("#catalogue-picker-modal-available-label").TextContent.Should().Contain("Available");
        cut.Markup.IndexOf("catalogue-picker-modal-available-label", StringComparison.Ordinal)
            .Should().BeLessThan(cut.Markup.IndexOf("catalogue-picker-modal-selected-label", StringComparison.Ordinal));
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
        cut.Find("#catalogue-picker-modal-selected-label").TextContent.Should().Contain("Sélection");
        cut.Find("#catalogue-picker-modal-available-label").TextContent.Should().Contain("Options disponibles");
        cut.Find("#catalogue-picker-modal-cancel").TextContent.Should().Contain("Annuler");
    }

    [Fact]
    public async Task ItShouldSearchTheCurrentLanguageDisplayName()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        await using var context = CreateContext();
        var (cut, _) = await ShowAsync(context, new CataloguePickerModalModel(
            "Espèces",
            [
                new CatalogueOptionModel(PikeSpeciesId, "Pike", "Brochet"),
                new CatalogueOptionModel(TenchSpeciesId, "Tench", "Tanche")
            ],
            AllowMultiple: true));

        // Act
        cut.Find("#catalogue-picker-modal-search").Input("bro");

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#catalogue-picker-modal-option-Pike").Should().NotBeNull();
            cut.FindAll("#catalogue-picker-modal-option-Tench").Should().BeEmpty();
        });
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
        await cut.Find("#catalogue-picker-modal-save").ClickAsync();
        var result = await dialog.Result;

        // Assert
        result!.Canceled.Should().BeFalse();
        result.Data.Should().BeOfType<CataloguePickerModalResult>();
        ((CataloguePickerModalResult)result.Data!).Options.Single().Id.Should().Be(PikeSpeciesId);
        ((CataloguePickerModalResult)result.Data!).Options.Single().Name.Should().Be("Pike");
    }

    [Fact]
    public async Task ItShouldKeepMultipleSelectionsUntilTheyAreSaved()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var model = DefaultModel() with { AllowMultiple = true };
        var (cut, dialog) = await ShowAsync(context, model);

        // Act
        await cut.Find("#catalogue-picker-modal-option-Pike").ClickAsync();
        await cut.Find("#catalogue-picker-modal-option-Tench").ClickAsync();
        await cut.Find("#catalogue-picker-modal-save").ClickAsync();
        var result = await dialog.Result;

        // Assert
        var selected = ((CataloguePickerModalResult)result!.Data!).Options;
        selected.Select(option => option.Id).Should().BeEquivalentTo([PikeSpeciesId, TenchSpeciesId]);
    }

    [Fact]
    public async Task ItShouldSearchTheCompleteCatalogueBeyondTheInitialLimit()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var options = Enumerable.Range(1, 25)
            .Select(number => new CatalogueOptionModel(
                Guid.Parse($"cccccccc-0000-0000-0000-{number:D12}"),
                $"Species{number}",
                $"Species {number}"))
            .ToArray();
        var (cut, dialog) = await ShowAsync(context, new CataloguePickerModalModel(
            "Species", options, AllowMultiple: true, ItemPluralName: "species"));

        // Act
        cut.FindAll("#catalogue-picker-modal-options .mud-chip").Should().HaveCount(20);
        cut.Find("#catalogue-picker-modal-limit-message").TextContent
            .Should().Contain("Showing 20 of 25 species. Search to find more.");
        cut.Find("#catalogue-picker-modal-search").Input("Species 25");
        cut.WaitForAssertion(() => cut.Find("#catalogue-picker-modal-option-Species25"));
        await cut.Find("#catalogue-picker-modal-option-Species25").ClickAsync();
        await cut.Find("#catalogue-picker-modal-save").ClickAsync();
        var result = await dialog.Result;

        // Assert
        ((CataloguePickerModalResult)result!.Data!).Options.Single().Code.Should().Be("Species25");
    }

    [Fact]
    public async Task ItShouldNotShowALimitMessageForTwentyOptions()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var options = Enumerable.Range(1, 20)
            .Select(number => new CatalogueOptionModel(Guid.NewGuid(), $"Species{number}", $"Species {number}"))
            .ToArray();

        // Act
        var (cut, _) = await ShowAsync(context, new CataloguePickerModalModel(
            "Species", options, AllowMultiple: true, ItemPluralName: "species"));

        // Assert
        cut.FindAll("#catalogue-picker-modal-limit-message").Should().BeEmpty();
        cut.FindAll("#catalogue-picker-modal-options .mud-chip").Should().HaveCount(20);
    }

    [Fact]
    public async Task ItShouldShowExistingSelectionsAndCancelWithoutReturningChanges()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var model = DefaultModel() with
        {
            AllowMultiple = true,
            SelectedOptionIds = new HashSet<Guid> { BrownTroutSpeciesId }
        };
        var (cut, dialog) = await ShowAsync(context, model);

        // Act
        cut.Find("#catalogue-picker-modal-selected-BrownTrout").Should().NotBeNull();
        await cut.Find("#catalogue-picker-modal-option-Pike").ClickAsync();
        await cut.Find("#catalogue-picker-modal-cancel").ClickAsync();
        var result = await dialog.Result;

        // Assert
        result!.Canceled.Should().BeTrue();
        result.Data.Should().BeNull();
    }
}
