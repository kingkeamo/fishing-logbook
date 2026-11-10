using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Pages.CatchList;
using FishingLogBook.Web.Localization;
using MudBlazor;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.CatchListTests;

public class WhenTestingDateRangeFilters : BaseCatchListTest
{
    private static readonly Guid TodayCatch = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    private static readonly Guid SixDaysAgoCatch = Guid.Parse("a0000000-0000-0000-0000-000000000002");
    private static readonly Guid SevenDaysAgoCatch = Guid.Parse("a0000000-0000-0000-0000-000000000003");
    private static readonly Guid TwentyNineDaysAgoCatch = Guid.Parse("a0000000-0000-0000-0000-000000000004");
    private static readonly Guid ThirtyDaysAgoCatch = Guid.Parse("a0000000-0000-0000-0000-000000000005");

    private static ICatchStore StoreAroundToday()
    {
        var store = Substitute.For<ICatchStore>();
        store.GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<CatchModel>>(
            [
                StoredCatch(TodayCatch, DateTimeOffset.Parse("2026-08-17T08:00:00Z"), speciesName: "Pike"),
                StoredCatch(SixDaysAgoCatch, DateTimeOffset.Parse("2026-08-11T08:00:00Z"), speciesName: "Perch"),
                StoredCatch(SevenDaysAgoCatch, DateTimeOffset.Parse("2026-08-10T08:00:00Z"), speciesName: "Roach"),
                StoredCatch(TwentyNineDaysAgoCatch, DateTimeOffset.Parse("2026-07-19T08:00:00Z"), speciesName: "Bream"),
                StoredCatch(ThirtyDaysAgoCatch, DateTimeOffset.Parse("2026-07-18T08:00:00Z"), speciesName: "Tench")
            ]);
        return store;
    }

    private static async Task<(IRenderedComponent<CatchList> Cut, IRenderedComponent<MudPopoverProvider> Popover)>
        RenderAndOpenFiltersAsync(BunitContext context)
    {
        var popover = context.Render<MudPopoverProvider>();
        var cut = context.Render<CatchList>();
        cut.WaitForAssertion(() => cut.Find("#catch-filters-button").Should().NotBeNull());
        await cut.Find("#catch-filters-button").ClickAsync();
        popover.WaitForAssertion(() => popover.Find("#catch-filter-date-Today").Should().NotBeNull());
        return (cut, popover);
    }

    [Fact]
    public async Task ItShouldShowOnlyCatchesFromTodayWhenTheTodayRangeIsChosen()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext(StoreAroundToday(), time: FixedTodayTime("2026-08-17"));
        var (cut, popover) = await RenderAndOpenFiltersAsync(context);

        // Act
        await popover.Find("#catch-filter-date-Today").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find($"#catch-card-{TodayCatch:D}").Should().NotBeNull();
            cut.FindAll($"#catch-card-{SixDaysAgoCatch:D}").Should().BeEmpty();
            cut.FindAll($"#catch-card-{SevenDaysAgoCatch:D}").Should().BeEmpty();
            cut.FindAll($"#catch-card-{TwentyNineDaysAgoCatch:D}").Should().BeEmpty();
            cut.FindAll($"#catch-card-{ThirtyDaysAgoCatch:D}").Should().BeEmpty();
        });
    }

    [Fact]
    public async Task ItShouldIncludeTheSixthDayBackButNotTheSeventhWhenTheLastSevenDaysRangeIsChosen()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext(StoreAroundToday(), time: FixedTodayTime("2026-08-17"));
        var (cut, popover) = await RenderAndOpenFiltersAsync(context);

        // Act
        await popover.Find("#catch-filter-date-Last7Days").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find($"#catch-card-{TodayCatch:D}").Should().NotBeNull();
            cut.Find($"#catch-card-{SixDaysAgoCatch:D}").Should().NotBeNull();
            cut.FindAll($"#catch-card-{SevenDaysAgoCatch:D}").Should().BeEmpty();
            cut.FindAll($"#catch-card-{TwentyNineDaysAgoCatch:D}").Should().BeEmpty();
        });
    }

    [Fact]
    public async Task ItShouldIncludeTheTwentyNinthDayBackButNotTheThirtiethWhenTheLastThirtyDaysRangeIsChosen()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext(StoreAroundToday(), time: FixedTodayTime("2026-08-17"));
        var (cut, popover) = await RenderAndOpenFiltersAsync(context);

        // Act
        await popover.Find("#catch-filter-date-Last30Days").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find($"#catch-card-{TodayCatch:D}").Should().NotBeNull();
            cut.Find($"#catch-card-{SevenDaysAgoCatch:D}").Should().NotBeNull();
            cut.Find($"#catch-card-{TwentyNineDaysAgoCatch:D}").Should().NotBeNull();
            cut.FindAll($"#catch-card-{ThirtyDaysAgoCatch:D}").Should().BeEmpty();
        });
    }

    [Fact]
    public async Task ItShouldShowEveryCatchAgainWhenTheActiveRangeIsChosenASecondTime()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext(StoreAroundToday(), time: FixedTodayTime("2026-08-17"));
        var (cut, popover) = await RenderAndOpenFiltersAsync(context);
        await popover.Find("#catch-filter-date-Today").ClickAsync();
        cut.WaitForAssertion(() => cut.FindAll($"#catch-card-{ThirtyDaysAgoCatch:D}").Should().BeEmpty());

        // Act
        await popover.Find("#catch-filter-date-Today").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find($"#catch-card-{TodayCatch:D}").Should().NotBeNull();
            cut.Find($"#catch-card-{ThirtyDaysAgoCatch:D}").Should().NotBeNull();
        });
    }
}
