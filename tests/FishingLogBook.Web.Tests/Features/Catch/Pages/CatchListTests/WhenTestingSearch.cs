using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Pages.CatchList;
using FishingLogBook.Web.Localization;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.CatchListTests;

public class WhenTestingSearch : BaseCatchListTest
{
    private static ICatchStore StoreWithSampleCatches(out Guid flyPike, out Guid spinningPerch, out Guid wormRoach)
    {
        flyPike = Guid.NewGuid();
        spinningPerch = Guid.NewGuid();
        wormRoach = Guid.NewGuid();
        var store = Substitute.For<ICatchStore>();
        store.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<CatchModel>>(
            [
                StoredCatch(flyPike, DateTimeOffset.Parse("2026-08-17T08:00:00Z"), speciesName: "Pike", method: "Fly"),
                StoredCatch(spinningPerch, DateTimeOffset.Parse("2026-08-16T08:00:00Z"), speciesName: "Perch", method: "Spinning"),
                StoredCatch(wormRoach, DateTimeOffset.Parse("2026-08-15T08:00:00Z"), speciesName: "Roach", method: "Bait", baitOrLure: "Worm")
            ]);
        return store;
    }

    [Fact]
    public async Task ItShouldFilterBySpecies()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = StoreWithSampleCatches(out var flyPike, out var spinningPerch, out var wormRoach);
        await using var context = CreateContext(store);
        var cut = context.Render<CatchList>();
        cut.WaitForAssertion(() => cut.Find("#catch-search").Should().NotBeNull());

        // Act
        cut.Find("#catch-search").Input("pike");

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find($"#catch-card-{flyPike:D}").Should().NotBeNull();
            cut.FindAll($"#catch-card-{spinningPerch:D}").Should().BeEmpty();
            cut.FindAll($"#catch-card-{wormRoach:D}").Should().BeEmpty();
        });
    }

    [Fact]
    public async Task ItShouldFilterByFishingMethod()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = StoreWithSampleCatches(out var flyPike, out var spinningPerch, out _);
        await using var context = CreateContext(store);
        var cut = context.Render<CatchList>();
        cut.WaitForAssertion(() => cut.Find("#catch-search").Should().NotBeNull());

        // Act
        cut.Find("#catch-search").Input("spinning");

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find($"#catch-card-{spinningPerch:D}").Should().NotBeNull();
            cut.FindAll($"#catch-card-{flyPike:D}").Should().BeEmpty();
        });
    }

    [Fact]
    public async Task ItShouldFilterByBaitOrLure()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = StoreWithSampleCatches(out var flyPike, out _, out var wormRoach);
        await using var context = CreateContext(store);
        var cut = context.Render<CatchList>();
        cut.WaitForAssertion(() => cut.Find("#catch-search").Should().NotBeNull());

        // Act
        cut.Find("#catch-search").Input("worm");

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find($"#catch-card-{wormRoach:D}").Should().NotBeNull();
            cut.FindAll($"#catch-card-{flyPike:D}").Should().BeEmpty();
        });
    }

    [Fact]
    public async Task ItShouldMatchCaseInsensitively()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = StoreWithSampleCatches(out var flyPike, out _, out _);
        await using var context = CreateContext(store);
        var cut = context.Render<CatchList>();
        cut.WaitForAssertion(() => cut.Find("#catch-search").Should().NotBeNull());

        // Act
        cut.Find("#catch-search").Input("PIKE");

        // Assert
        cut.WaitForAssertion(() => cut.Find($"#catch-card-{flyPike:D}").Should().NotBeNull());
    }

    [Fact]
    public async Task ItShouldRestoreAllCatchesWhenSearchIsCleared()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = StoreWithSampleCatches(out var flyPike, out var spinningPerch, out var wormRoach);
        await using var context = CreateContext(store);
        var cut = context.Render<CatchList>();
        cut.WaitForAssertion(() => cut.Find("#catch-search").Should().NotBeNull());
        cut.Find("#catch-search").Input("pike");
        cut.WaitForAssertion(() => cut.FindAll($"#catch-card-{spinningPerch:D}").Should().BeEmpty());

        // Act
        cut.Find("#catch-search").Input(string.Empty);

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find($"#catch-card-{flyPike:D}").Should().NotBeNull();
            cut.Find($"#catch-card-{spinningPerch:D}").Should().NotBeNull();
            cut.Find($"#catch-card-{wormRoach:D}").Should().NotBeNull();
        });
    }
}
