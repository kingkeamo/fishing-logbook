using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Catch.Components.CatchFilters;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Localization;
using MudBlazor;

namespace FishingLogBook.Web.Tests.Features.Catch.Components.CatchFiltersTests;

public class WhenTestingRender : BaseCatchFiltersTest
{
    [Fact]
    public async Task ItShouldRenderTheSearchFieldAndQuickMethodChips()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<CatchFilters>(parameters => parameters
            .Add(filters => filters.Filters, new CatchFilterModel())
            .Add(filters => filters.MethodOptions, new[] { "Fly", "Spinning" })
            .Add(filters => filters.SpeciesOptions, new[] { "Pike", "Brown Trout" }));

        // Assert
        cut.Find("#catch-search").GetAttribute("aria-label").Should().Be("Search catches");
        cut.Find("#catch-filter-method-all").ClassList.Should().Contain("mud-chip-filled");
        cut.Find("#catch-filter-method-Fly").Should().NotBeNull();
        cut.Find("#catch-filter-method-Spinning").Should().NotBeNull();
    }

    [Fact]
    public async Task ItShouldHideActiveFiltersWhenNoneAreSet()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<CatchFilters>(parameters => parameters
            .Add(filters => filters.Filters, new CatchFilterModel())
            .Add(filters => filters.MethodOptions, [])
            .Add(filters => filters.SpeciesOptions, []));

        // Assert
        cut.FindAll("#catch-active-filters").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldShowRemovableActiveFilterChips()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<CatchFilters>(parameters => parameters
            .Add(filters => filters.Filters, new CatchFilterModel("pike", "Fly", "Pike", CatchDateRangeFilter.Today))
            .Add(filters => filters.MethodOptions, ["Fly"])
            .Add(filters => filters.SpeciesOptions, ["Pike"]));

        // Assert
        cut.Find("#catch-active-filter-search").TextContent.Should().Contain("pike");
        cut.Find("#catch-active-filter-method").TextContent.Should().Contain("Fly");
        cut.Find("#catch-active-filter-species").TextContent.Should().Contain("Pike");
        cut.Find("#catch-active-filter-date").TextContent.Should().Contain("Today");
        cut.Find("#catch-clear-all-filters").Should().NotBeNull();
    }

    [Fact]
    public async Task ItShouldShowSpeciesAndDateRangeOptionsInTheFiltersMenu()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var popover = context.Render<MudPopoverProvider>();
        var cut = context.Render<CatchFilters>(parameters => parameters
            .Add(filters => filters.Filters, new CatchFilterModel())
            .Add(filters => filters.MethodOptions, [])
            .Add(filters => filters.SpeciesOptions, ["Pike", "Brown Trout"]));

        // Act
        await cut.Find("#catch-filters-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            popover.Find("#catch-filter-species-Pike").Should().NotBeNull();
            popover.Find("#catch-filter-date-Today").TextContent.Should().Contain("Today");
            popover.Find("#catch-filters-menu-clear-all").Should().NotBeNull();
        });
    }
}
