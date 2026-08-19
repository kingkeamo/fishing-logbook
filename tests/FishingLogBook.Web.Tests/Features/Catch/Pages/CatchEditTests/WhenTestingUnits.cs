using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Pages.CatchEdit;
using FishingLogBook.Web.Localization;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.CatchEditTests;

public class WhenTestingUnits : BaseCatchEditTest
{
    private static readonly Guid EditedCatchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task ItShouldLabelTheMeasurementsInMetricByDefault()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, EditedCatchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(EditedCatchId, weight: 2.041m, length: 46.36m));
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(page => page.CatchId, EditedCatchId));

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#catch-edit-weight").GetAttribute("value").Should().Be("2.041");
            cut.Find("#catch-edit-length").GetAttribute("value").Should().Be("46.36");
            cut.Markup.Should().Contain("Weight (kg)");
            cut.Markup.Should().Contain("Length (cm)");
        });
    }

    [Fact]
    public async Task ItShouldShowTheStoredMeasurementsInTheAnglersPreferredUnits()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, EditedCatchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(EditedCatchId, weight: 2.041m, length: 46.36m));
        var preferences = QuietAnglerPreferences(weightUnit: WeightUnitEnum.Lb, lengthUnit: LengthUnitEnum.In);
        await using var context = CreateContext(store, anglerPreferences: preferences);

        // Act
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(page => page.CatchId, EditedCatchId));

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#catch-edit-weight").GetAttribute("value").Should().Be("4.50");
            cut.Find("#catch-edit-length").GetAttribute("value").Should().Be("18.25");
            cut.Markup.Should().Contain("Weight (lb)");
            cut.Markup.Should().Contain("Length (in)");
        });
        await preferences.Received(1).GetAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStateTheWeightLimitInTheAnglersPreferredUnit()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, EditedCatchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(EditedCatchId, weight: 2.041m));
        var preferences = QuietAnglerPreferences(weightUnit: WeightUnitEnum.Lb, lengthUnit: LengthUnitEnum.In);
        await using var context = CreateContext(store, anglerPreferences: preferences);
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(page => page.CatchId, EditedCatchId));
        cut.WaitForAssertion(() => cut.Find("#catch-edit-weight"));
        cut.Find("#catch-edit-weight").Input("5000");

        // Act
        await cut.Find("#catch-edit-save").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-edit-validation").TextContent
            .Should().Contain("Weight must be greater than 0 lb and at most 2204.62 lb."));
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPersistCanonicalKilogramsAndCentimetresWhenEnteredInImperialUnits()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, EditedCatchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(EditedCatchId));
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var preferences = QuietAnglerPreferences(weightUnit: WeightUnitEnum.Lb, lengthUnit: LengthUnitEnum.In);
        await using var context = CreateContext(store, anglerPreferences: preferences);
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(page => page.CatchId, EditedCatchId));
        cut.WaitForAssertion(() => cut.Find("#catch-edit-weight"));
        cut.Find("#catch-edit-weight").Input("4.50");
        cut.Find("#catch-edit-length").Input("18.25");

        // Act
        await cut.Find("#catch-edit-save").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-edit-saved").Should().NotBeNull());
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Id == EditedCatchId
                && catchRecord.Weight == 2.041m
                && catchRecord.Length == 46.36m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotChangeTheStoredMeasurementsWhenSavingWithoutEditingThem()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        const decimal storedWeight = 2.0413m;
        const decimal storedLength = 46.357m;
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, EditedCatchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(EditedCatchId, weight: storedWeight, length: storedLength));
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var preferences = QuietAnglerPreferences(weightUnit: WeightUnitEnum.Lb, lengthUnit: LengthUnitEnum.In);
        await using var context = CreateContext(store, anglerPreferences: preferences);
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(page => page.CatchId, EditedCatchId));
        cut.WaitForAssertion(() => cut.Find("#catch-edit-weight"));
        cut.Find("#catch-edit-notes").Input("Great fight");

        // Act
        await cut.Find("#catch-edit-save").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-edit-saved").Should().NotBeNull());
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Weight == storedWeight
                && catchRecord.Length == storedLength
                && catchRecord.Notes == "Great fight"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowFrenchUnitLabels()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, EditedCatchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(EditedCatchId, weight: 2.041m, length: 46.36m));
        var preferences = QuietAnglerPreferences(weightUnit: WeightUnitEnum.Lb, lengthUnit: LengthUnitEnum.In);
        await using var context = CreateContext(store, anglerPreferences: preferences);

        // Act
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(page => page.CatchId, EditedCatchId));

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("(lb)");
            cut.Markup.Should().Contain("(po)");
        });
    }
}
