using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Catch.Components.CatchFilters;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FishingLogBook.Web.Tests.Features.Catch.Components.CatchFiltersTests;

public class WhenTestingToggles : BaseCatchFiltersTest
{
    [Fact]
    public async Task ItShouldDeselectTheActiveSpeciesWhenItIsChosenAgainIgnoringCase()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        CatchFilterModel? raised = null;
        await using var context = CreateContext();
        var popover = context.Render<MudPopoverProvider>();
        var cut = context.Render<CatchFilters>(parameters => parameters
            .Add(filters => filters.Filters, new CatchFilterModel(Species: "pike"))
            .Add(filters => filters.SpeciesOptions, ["Pike", "Perch"])
            .Add(filters => filters.FiltersChanged, EventCallback.Factory.Create<CatchFilterModel>(this, model => raised = model)));
        await cut.Find("#catch-filters-button").ClickAsync();
        popover.WaitForAssertion(() => popover.Find("#catch-filter-species-Pike").Should().NotBeNull());

        // Act
        await popover.Find("#catch-filter-species-Pike").ClickAsync();

        // Assert
        raised.Should().NotBeNull();
        raised!.Species.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldSwitchToAnotherSpeciesWithoutLosingTheOtherFilters()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        CatchFilterModel? raised = null;
        await using var context = CreateContext();
        var popover = context.Render<MudPopoverProvider>();
        var cut = context.Render<CatchFilters>(parameters => parameters
            .Add(filters => filters.Filters, new CatchFilterModel("trout", "Fly", "Pike", CatchDateRangeFilter.Last7Days))
            .Add(filters => filters.SpeciesOptions, ["Pike", "Perch"])
            .Add(filters => filters.FiltersChanged, EventCallback.Factory.Create<CatchFilterModel>(this, model => raised = model)));
        await cut.Find("#catch-filters-button").ClickAsync();
        popover.WaitForAssertion(() => popover.Find("#catch-filter-species-Perch").Should().NotBeNull());

        // Act
        await popover.Find("#catch-filter-species-Perch").ClickAsync();

        // Assert
        raised.Should().Be(new CatchFilterModel("trout", "Fly", "Perch", CatchDateRangeFilter.Last7Days));
    }

    [Fact]
    public async Task ItShouldDeselectTheActiveMethodWhenItIsChosenAgainIgnoringCase()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        CatchFilterModel? raised = null;
        await using var context = CreateContext();
        var cut = context.Render<CatchFilters>(parameters => parameters
            .Add(filters => filters.Filters, new CatchFilterModel(Method: "fly"))
            .Add(filters => filters.MethodOptions, ["Fly", "Spinning"])
            .Add(filters => filters.FiltersChanged, EventCallback.Factory.Create<CatchFilterModel>(this, model => raised = model)));

        // Act
        await cut.Find("#catch-filter-method-Fly").ClickAsync();

        // Assert
        raised.Should().NotBeNull();
        raised!.Method.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldDeselectTheActiveDateRangeWhenItIsChosenAgain()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        CatchFilterModel? raised = null;
        await using var context = CreateContext();
        var popover = context.Render<MudPopoverProvider>();
        var cut = context.Render<CatchFilters>(parameters => parameters
            .Add(filters => filters.Filters, new CatchFilterModel(DateRange: CatchDateRangeFilter.Last30Days))
            .Add(filters => filters.FiltersChanged, EventCallback.Factory.Create<CatchFilterModel>(this, model => raised = model)));
        await cut.Find("#catch-filters-button").ClickAsync();
        popover.WaitForAssertion(() => popover.Find("#catch-filter-date-Last30Days").Should().NotBeNull());

        // Act
        await popover.Find("#catch-filter-date-Last30Days").ClickAsync();

        // Assert
        raised.Should().NotBeNull();
        raised!.DateRange.Should().Be(CatchDateRangeFilter.All);
    }
}
