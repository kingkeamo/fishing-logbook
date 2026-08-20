using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Catch.Components.CatchFilters;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FishingLogBook.Web.Tests.Features.Catch.Components.CatchFiltersTests;

public class WhenTestingFilterChanges : BaseCatchFiltersTest
{
    [Fact]
    public async Task ItShouldRaiseFiltersChangedWhenSearchTextChanges()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        CatchFilterModel? raised = null;
        await using var context = CreateContext();
        var cut = context.Render<CatchFilters>(parameters => parameters
            .Add(filters => filters.Filters, new CatchFilterModel())
            .Add(filters => filters.MethodOptions, [])
            .Add(filters => filters.SpeciesOptions, [])
            .Add(filters => filters.FiltersChanged, EventCallback.Factory.Create<CatchFilterModel>(this, model => raised = model)));

        // Act
        cut.Find("#catch-search").Input("pike");

        // Assert
        raised.Should().NotBeNull();
        raised!.SearchTerm.Should().Be("pike");
    }

    [Fact]
    public async Task ItShouldSelectAMethodChipAndDeselectItOnASecondClick()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        CatchFilterModel filters = new();
        await using var context = CreateContext();
        var cut = context.Render<CatchFilters>(parameters => parameters
            .Add(f => f.Filters, filters)
            .Add(f => f.MethodOptions, ["Fly"])
            .Add(f => f.SpeciesOptions, [])
            .Add(f => f.FiltersChanged, EventCallback.Factory.Create<CatchFilterModel>(this, model => filters = model)));

        // Act
        await cut.Find("#catch-filter-method-Fly").ClickAsync();

        // Assert
        filters.Method.Should().Be("Fly");
    }

    [Fact]
    public async Task ItShouldClearTheMethodWhenAllIsClicked()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        CatchFilterModel filters = new(Method: "Fly");
        await using var context = CreateContext();
        var cut = context.Render<CatchFilters>(parameters => parameters
            .Add(f => f.Filters, filters)
            .Add(f => f.MethodOptions, ["Fly"])
            .Add(f => f.SpeciesOptions, [])
            .Add(f => f.FiltersChanged, EventCallback.Factory.Create<CatchFilterModel>(this, model => filters = model)));

        // Act
        await cut.Find("#catch-filter-method-all").ClickAsync();

        // Assert
        filters.Method.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldSelectASpeciesFromTheFiltersMenu()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        CatchFilterModel filters = new();
        await using var context = CreateContext();
        var popover = context.Render<MudPopoverProvider>();
        var cut = context.Render<CatchFilters>(parameters => parameters
            .Add(f => f.Filters, filters)
            .Add(f => f.MethodOptions, [])
            .Add(f => f.SpeciesOptions, ["Pike"])
            .Add(f => f.FiltersChanged, EventCallback.Factory.Create<CatchFilterModel>(this, model => filters = model)));
        await cut.Find("#catch-filters-button").ClickAsync();
        cut.WaitForAssertion(() => popover.Find("#catch-filter-species-Pike").Should().NotBeNull());

        // Act
        await popover.Find("#catch-filter-species-Pike").ClickAsync();

        // Assert
        filters.Species.Should().Be("Pike");
    }

    [Fact]
    public async Task ItShouldSelectADateRangeFromTheFiltersMenu()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        CatchFilterModel filters = new();
        await using var context = CreateContext();
        var popover = context.Render<MudPopoverProvider>();
        var cut = context.Render<CatchFilters>(parameters => parameters
            .Add(f => f.Filters, filters)
            .Add(f => f.MethodOptions, [])
            .Add(f => f.SpeciesOptions, [])
            .Add(f => f.FiltersChanged, EventCallback.Factory.Create<CatchFilterModel>(this, model => filters = model)));
        await cut.Find("#catch-filters-button").ClickAsync();
        cut.WaitForAssertion(() => popover.Find("#catch-filter-date-Today").Should().NotBeNull());

        // Act
        await popover.Find("#catch-filter-date-Today").ClickAsync();

        // Assert
        filters.DateRange.Should().Be(CatchDateRangeFilter.Today);
    }

    [Fact]
    public async Task ItShouldResetAllFiltersWhenClearAllIsClicked()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        CatchFilterModel filters = new("pike", "Fly", "Pike", CatchDateRangeFilter.Today);
        await using var context = CreateContext();
        var cut = context.Render<CatchFilters>(parameters => parameters
            .Add(f => f.Filters, filters)
            .Add(f => f.MethodOptions, ["Fly"])
            .Add(f => f.SpeciesOptions, ["Pike"])
            .Add(f => f.FiltersChanged, EventCallback.Factory.Create<CatchFilterModel>(this, model => filters = model)));

        // Act
        await cut.Find("#catch-clear-all-filters").ClickAsync();

        // Assert
        filters.Should().Be(new CatchFilterModel());
    }

    [Fact]
    public async Task ItShouldClearOnlyTheSearchFilterWhenItsChipIsClosed()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        CatchFilterModel filters = new("pike", "Fly", null, CatchDateRangeFilter.All);
        await using var context = CreateContext();
        var cut = context.Render<CatchFilters>(parameters => parameters
            .Add(f => f.Filters, filters)
            .Add(f => f.MethodOptions, ["Fly"])
            .Add(f => f.SpeciesOptions, [])
            .Add(f => f.FiltersChanged, EventCallback.Factory.Create<CatchFilterModel>(this, model => filters = model)));

        // Act
        await cut.Find("#catch-active-filter-search .mud-chip-close-button").ClickAsync();

        // Assert
        filters.SearchTerm.Should().BeEmpty();
        filters.Method.Should().Be("Fly");
    }
}
