using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Pages.CatchEdit;
using FishingLogBook.Web.Localization;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.CatchEditTests;

public class WhenTestingChips : BaseCatchEditTest
{
    private static readonly Guid EditedCatchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task ItShouldShowAHintWhenTheCatalogueIsUnavailable()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, EditedCatchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(EditedCatchId, speciesName: "Pike", method: "Trotting"));
        var preferences = QuietAnglerPreferences();
        await using var context = CreateContext(store, anglerPreferences: preferences);

        // Act
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(page => page.CatchId, EditedCatchId));

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#catch-edit-catalogue-unavailable").Should().NotBeNull();
            cut.Find("#catch-edit-species").GetAttribute("value").Should().Be("Pike");
            cut.Find("#catch-edit-method").GetAttribute("value").Should().Be("Trotting");
        });
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotOverwriteAnAlreadyRecordedMethodOrSpeciesWithTheProfileDefault()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, EditedCatchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(EditedCatchId, speciesName: "Pike", method: "Spinning"));
        var preferences = QuietAnglerPreferences(SamplePreferences(), SampleCatalogue());
        await using var context = CreateContext(store, anglerPreferences: preferences);

        // Act
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(page => page.CatchId, EditedCatchId));

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#catch-edit-method").GetAttribute("value").Should().Be("Spinning");
            cut.Find("#catch-edit-species").GetAttribute("value").Should().Be("Pike");
            cut.Find("#catch-edit-method-Spinning").ClassList.Should().Contain("mud-chip-filled");
        });
        await preferences.Received(1).GetAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSelectTheProfileDefaultsWhenBothStoredFieldsAreEmpty()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, EditedCatchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(EditedCatchId));
        var preferences = QuietAnglerPreferences(SamplePreferences(), SampleCatalogue());
        await using var context = CreateContext(store, anglerPreferences: preferences);

        // Act
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(page => page.CatchId, EditedCatchId));

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#catch-edit-method").GetAttribute("value").Should().Be("Fly");
            cut.Find("#catch-edit-species").GetAttribute("value").Should().Be("Brown Trout");
            cut.Find("#catch-edit-method-Fly").ClassList.Should().Contain("mud-chip-filled");
            cut.Find("#catch-edit-species-BrownTrout").ClassList.Should().Contain("mud-chip-filled");
        });
        await preferences.Received(1).GetAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSelectTheDefaultSpeciesForAStoredMethodWhenOnlySpeciesIsEmpty()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, EditedCatchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(EditedCatchId, method: "Spinning"));
        var preferences = QuietAnglerPreferences(SamplePreferences(), SampleCatalogue());
        await using var context = CreateContext(store, anglerPreferences: preferences);

        // Act
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(page => page.CatchId, EditedCatchId));

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#catch-edit-method").GetAttribute("value").Should().Be("Spinning");
            cut.Find("#catch-edit-species").GetAttribute("value").Should().Be("Pike");
            cut.Find("#catch-edit-species-Pike").ClassList.Should().Contain("mud-chip-filled");
        });
    }

    [Fact]
    public async Task ItShouldLeaveTheFieldsEmptyWhenTheAnglerHasNoProfileDefaults()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, EditedCatchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(EditedCatchId));
        var preferences = QuietAnglerPreferences(
            new FishingLogBook.Shared.Dtos.FishingPreferencesDto([]),
            SampleCatalogue());
        await using var context = CreateContext(store, anglerPreferences: preferences);

        // Act
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(page => page.CatchId, EditedCatchId));

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#catch-edit-method").GetAttribute("value").Should().BeEmpty();
            cut.Find("#catch-edit-species").GetAttribute("value").Should().BeEmpty();
        });
    }

    [Fact]
    public async Task ItShouldApplyTheDefaultSpeciesWhenTheMethodChangesAndSpeciesIsEmpty()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, EditedCatchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(EditedCatchId, method: "Trotting"));
        var preferences = QuietAnglerPreferences(SamplePreferences(), SampleCatalogue());
        await using var context = CreateContext(store, anglerPreferences: preferences);
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(page => page.CatchId, EditedCatchId));
        cut.WaitForAssertion(() => cut.Find("#catch-edit-method-Fly"));

        // Act
        await cut.Find("#catch-edit-method-Spinning").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#catch-edit-method").GetAttribute("value").Should().Be("Spinning");
            cut.Find("#catch-edit-species").GetAttribute("value").Should().Be("Pike");
        });
    }

    [Fact]
    public async Task ItShouldReplaceAnAutoDefaultedSpeciesWhenTheMethodChanges()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, EditedCatchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(EditedCatchId));
        var preferences = QuietAnglerPreferences(SamplePreferences(), SampleCatalogue());
        await using var context = CreateContext(store, anglerPreferences: preferences);
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(page => page.CatchId, EditedCatchId));
        cut.WaitForAssertion(() =>
            cut.Find("#catch-edit-species").GetAttribute("value").Should().Be("Brown Trout"));

        // Act
        await cut.Find("#catch-edit-method-Spinning").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#catch-edit-method").GetAttribute("value").Should().Be("Spinning");
            cut.Find("#catch-edit-species").GetAttribute("value").Should().Be("Pike");
        });
    }

    [Fact]
    public async Task ItShouldClearAnAutoDefaultedSpeciesWhenTheNewMethodHasNoDefaultSpecies()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, EditedCatchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(EditedCatchId));
        var storedPreferences = new FishingLogBook.Shared.Dtos.FishingPreferencesDto(
        [
            new FishingLogBook.Shared.Dtos.FishingMethodPreferenceDto(
                FlyMethodId,
                "Fly",
                "Fly",
                true,
                [
                    new FishingLogBook.Shared.Dtos.FishingSpeciesPreferenceDto(
                        BrownTroutSpeciesId,
                        "BrownTrout",
                        "Brown Trout",
                        true)
                ]),
            new FishingLogBook.Shared.Dtos.FishingMethodPreferenceDto(
                SpinningMethodId,
                "Spinning",
                "Spinning",
                false,
                [])
        ]);
        var preferences = QuietAnglerPreferences(storedPreferences, SampleCatalogue());
        await using var context = CreateContext(store, anglerPreferences: preferences);
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(page => page.CatchId, EditedCatchId));
        cut.WaitForAssertion(() =>
            cut.Find("#catch-edit-species").GetAttribute("value").Should().Be("Brown Trout"));

        // Act
        await cut.Find("#catch-edit-method-Spinning").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#catch-edit-method").GetAttribute("value").Should().Be("Spinning");
            cut.Find("#catch-edit-species").GetAttribute("value").Should().BeEmpty();
        });
    }

    [Fact]
    public async Task ItShouldKeepAnExistingSpeciesWhenTheMethodChanges()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, EditedCatchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(EditedCatchId, speciesName: "Grayling", method: "Fly"));
        var preferences = QuietAnglerPreferences(SamplePreferences(), SampleCatalogue());
        await using var context = CreateContext(store, anglerPreferences: preferences);
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(page => page.CatchId, EditedCatchId));
        cut.WaitForAssertion(() => cut.Find("#catch-edit-method-Spinning"));

        // Act
        await cut.Find("#catch-edit-method-Spinning").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#catch-edit-method").GetAttribute("value").Should().Be("Spinning");
            cut.Find("#catch-edit-species").GetAttribute("value").Should().Be("Grayling");
        });
    }

    [Fact]
    public async Task ItShouldStillShowASavedSpeciesThatIsNotInTheShortlist()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, EditedCatchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(EditedCatchId, speciesName: "Grayling", method: "Fly"));
        var preferences = QuietAnglerPreferences(SamplePreferences(), SampleCatalogue());
        await using var context = CreateContext(store, anglerPreferences: preferences);

        // Act
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(page => page.CatchId, EditedCatchId));

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#catch-edit-species-Grayling").ClassList.Should().Contain("mud-chip-filled");
            cut.Find("#catch-edit-species-BrownTrout").Should().NotBeNull();
            cut.Find("#catch-edit-species").GetAttribute("value").Should().Be("Grayling");
        });
    }

    [Fact]
    public async Task ItShouldSaveTheSpeciesChosenFromAChip()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, EditedCatchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(EditedCatchId, method: "Fly"));
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var preferences = QuietAnglerPreferences(SamplePreferences(), SampleCatalogue());
        await using var context = CreateContext(store, anglerPreferences: preferences);
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(page => page.CatchId, EditedCatchId));
        cut.WaitForAssertion(() => cut.Find("#catch-edit-species-BrownTrout"));

        // Act
        await cut.Find("#catch-edit-species-BrownTrout").ClickAsync();
        await cut.Find("#catch-edit-save").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-edit-saved").Should().NotBeNull());
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Id == EditedCatchId
                && catchRecord.SpeciesName == "Brown Trout"
                && catchRecord.Method == "Fly"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldApplyTheMethodChosenFromTheFullCatalogue()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, EditedCatchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(EditedCatchId, method: "Fly"));
        var preferences = QuietAnglerPreferences(SamplePreferences(), SampleCatalogue());
        var modalService = Substitute.For<IModalService>();
        modalService
            .ShowAsync<CataloguePickerModal, CataloguePickerModalModel, CataloguePickerModalResult>(
                Arg.Any<CataloguePickerModalModel>(),
                Arg.Any<CancellationToken>())
            .Returns(new CataloguePickerModalResult(
                new CatalogueOptionModel(SpinningMethodId, "Spinning", "Spinning")));
        await using var context = CreateContext(
            store,
            anglerPreferences: preferences,
            modalService: modalService);
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(page => page.CatchId, EditedCatchId));
        cut.WaitForAssertion(() => cut.Find("#catch-edit-method-more"));

        // Act
        await cut.Find("#catch-edit-method-more").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-edit-method").GetAttribute("value").Should().Be("Spinning"));
        await modalService.Received(1)
            .ShowAsync<CataloguePickerModal, CataloguePickerModalModel, CataloguePickerModalResult>(
                Arg.Is<CataloguePickerModalModel>(model => model.Options.Count == 2),
                Arg.Any<CancellationToken>());
    }
}
