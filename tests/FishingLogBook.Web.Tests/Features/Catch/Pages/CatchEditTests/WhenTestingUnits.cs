using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Common.Modals;
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
            cut.Find("#catch-edit-weight-value").TextContent.Should().Be("2.04 kg");
            cut.Find("#catch-edit-length-value").TextContent.Should().Be("46.36 cm");
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
            cut.Find("#catch-edit-weight-value").TextContent.Should().Be("4 lb 8 oz");
            cut.Find("#catch-edit-length-value").TextContent.Should().Be("18.25 in");
        });
        await preferences.Received(1).GetAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotChangeTheWeightWhenTheMeasurementEditorIsCancelled()
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
        await cut.Find("#catch-edit-weight").ClickAsync();

        // Act
        await cut.Find("#catch-edit-save").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-edit-saved").Should().NotBeNull());
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord => catchRecord.Weight == 2.041m),
            Arg.Any<CancellationToken>());
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
        var modal = Substitute.For<IModalService>();
        AnswerMeasurement(modal, true, 2.041m);
        AnswerMeasurement(modal, false, 46.36m);
        await using var context = CreateContext(store, anglerPreferences: preferences, modalService: modal);
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(page => page.CatchId, EditedCatchId));
        cut.WaitForAssertion(() => cut.Find("#catch-edit-weight"));
        await cut.Find("#catch-edit-weight").ClickAsync();
        await cut.Find("#catch-edit-length").ClickAsync();

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
            cut.Find("#catch-edit-weight-value").TextContent.Should().Be("4 lb 8 oz");
            cut.Find("#catch-edit-length-value").TextContent.Should().Contain("18,25");
        });
    }
}
