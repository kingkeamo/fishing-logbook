using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Pages.CatchList;
using FishingLogBook.Web.Localization;
using MudBlazor;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.CatchListTests;

public class WhenTestingFilters : BaseCatchListTest
{
    private static ICatchStore StoreWithSampleCatches(out Guid flyPike, out Guid spinningPerch, out Guid flyRoach)
    {
        flyPike = Guid.NewGuid();
        spinningPerch = Guid.NewGuid();
        flyRoach = Guid.NewGuid();
        var store = Substitute.For<ICatchStore>();
        store.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<CatchModel>>(
            [
                StoredCatch(flyPike, DateTimeOffset.Parse("2026-08-17T08:00:00Z"), speciesName: "Pike", method: "Fly"),
                StoredCatch(spinningPerch, DateTimeOffset.Parse("2026-08-16T08:00:00Z"), speciesName: "Perch", method: "Spinning"),
                StoredCatch(flyRoach, DateTimeOffset.Parse("2026-08-15T08:00:00Z"), speciesName: "Roach", method: "Fly")
            ]);
        return store;
    }

    [Fact]
    public async Task ItShouldFilterByFishingMethodQuickChip()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = StoreWithSampleCatches(out var flyPike, out var spinningPerch, out var flyRoach);
        await using var context = CreateContext(store);
        var cut = context.Render<CatchList>();
        cut.WaitForAssertion(() => cut.Find("#catch-filter-method-Fly").Should().NotBeNull());

        // Act
        await cut.Find("#catch-filter-method-Fly").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find($"#catch-card-{flyPike:D}").Should().NotBeNull();
            cut.Find($"#catch-card-{flyRoach:D}").Should().NotBeNull();
            cut.FindAll($"#catch-card-{spinningPerch:D}").Should().BeEmpty();
        });
    }

    [Fact]
    public async Task ItShouldFilterBySpeciesFromTheFiltersMenu()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = StoreWithSampleCatches(out var flyPike, out var spinningPerch, out _);
        await using var context = CreateContext(store);
        var popover = context.Render<MudPopoverProvider>();
        var cut = context.Render<CatchList>();
        cut.WaitForAssertion(() => cut.Find("#catch-filters-button").Should().NotBeNull());
        await cut.Find("#catch-filters-button").ClickAsync();
        cut.WaitForAssertion(() => popover.Find("#catch-filter-species-Pike").Should().NotBeNull());

        // Act
        await popover.Find("#catch-filter-species-Pike").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find($"#catch-card-{flyPike:D}").Should().NotBeNull();
            cut.FindAll($"#catch-card-{spinningPerch:D}").Should().BeEmpty();
        });
    }

    [Fact]
    public async Task ItShouldCombineSearchAndMethodFilters()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = StoreWithSampleCatches(out var flyPike, out _, out var flyRoach);
        await using var context = CreateContext(store);
        var cut = context.Render<CatchList>();
        cut.WaitForAssertion(() => cut.Find("#catch-filter-method-Fly").Should().NotBeNull());
        await cut.Find("#catch-filter-method-Fly").ClickAsync();

        // Act
        cut.Find("#catch-search").Input("pike");

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find($"#catch-card-{flyPike:D}").Should().NotBeNull();
            cut.FindAll($"#catch-card-{flyRoach:D}").Should().BeEmpty();
        });
    }

    [Fact]
    public async Task ItShouldShowNoMatchesStateAndClearFiltersRestoresTheList()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = StoreWithSampleCatches(out var flyPike, out _, out _);
        await using var context = CreateContext(store);
        var cut = context.Render<CatchList>();
        cut.WaitForAssertion(() => cut.Find("#catch-search").Should().NotBeNull());
        cut.Find("#catch-search").Input("no such species anywhere");

        // Act
        cut.WaitForAssertion(() => cut.Find("#catch-list-no-matches").Should().NotBeNull());
        await cut.Find("#catch-list-clear-filters").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.FindAll("#catch-list-no-matches").Should().BeEmpty();
            cut.Find($"#catch-card-{flyPike:D}").Should().NotBeNull();
        });
    }

    [Fact]
    public async Task ItShouldClearAllActiveFiltersFromTheActiveFilterRow()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = StoreWithSampleCatches(out var flyPike, out var spinningPerch, out var flyRoach);
        await using var context = CreateContext(store);
        var cut = context.Render<CatchList>();
        cut.WaitForAssertion(() => cut.Find("#catch-filter-method-Fly").Should().NotBeNull());
        await cut.Find("#catch-filter-method-Fly").ClickAsync();
        cut.WaitForAssertion(() => cut.Find("#catch-clear-all-filters").Should().NotBeNull());

        // Act
        await cut.Find("#catch-clear-all-filters").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find($"#catch-card-{flyPike:D}").Should().NotBeNull();
            cut.Find($"#catch-card-{spinningPerch:D}").Should().NotBeNull();
            cut.Find($"#catch-card-{flyRoach:D}").Should().NotBeNull();
        });
    }
}
