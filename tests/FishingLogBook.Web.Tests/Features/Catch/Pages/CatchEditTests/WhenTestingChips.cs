using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
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
            cut.Find("#catch-edit-species-Pike").ClassList.Should().Contain("mud-chip-filled");
            cut.Find("#catch-edit-method-Trotting").ClassList.Should().Contain("mud-chip-filled");
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
            cut.Find("#catch-edit-method-Spinning").ClassList.Should().Contain("mud-chip-filled");
            cut.Find("#catch-edit-species-Pike").ClassList.Should().Contain("mud-chip-filled");
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
            cut.Find("#catch-edit-method-Spinning").ClassList.Should().Contain("mud-chip-filled");
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
            cut.Find("#catch-edit-method-chips").QuerySelectorAll(".mud-chip-filled").Should().BeEmpty();
            cut.Find("#catch-edit-species-chips").QuerySelectorAll(".mud-chip-filled").Should().BeEmpty();
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
            cut.Find("#catch-edit-method-Spinning").ClassList.Should().Contain("mud-chip-filled");
            cut.Find("#catch-edit-species-Pike").ClassList.Should().Contain("mud-chip-filled");
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
            cut.Find("#catch-edit-species-BrownTrout").ClassList.Should().Contain("mud-chip-filled"));

        // Act
        await cut.Find("#catch-edit-method-Spinning").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#catch-edit-method-Spinning").ClassList.Should().Contain("mud-chip-filled");
            cut.Find("#catch-edit-species-Pike").ClassList.Should().Contain("mud-chip-filled");
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
            cut.Find("#catch-edit-species-BrownTrout").ClassList.Should().Contain("mud-chip-filled"));

        // Act
        await cut.Find("#catch-edit-method-Spinning").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#catch-edit-method-Spinning").ClassList.Should().Contain("mud-chip-filled");
            cut.Find("#catch-edit-species-chips").QuerySelectorAll(".mud-chip-filled").Should().BeEmpty();
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
            cut.Find("#catch-edit-method-Spinning").ClassList.Should().Contain("mud-chip-filled");
            cut.Find("#catch-edit-species-Grayling").ClassList.Should().Contain("mud-chip-filled");
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
    public async Task ItShouldPreserveLegacyUnmappedValuesWhenSavingWithoutChanges()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, EditedCatchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(
                EditedCatchId,
                SyncStatus.Synchronised,
                SyncStatus.Synchronised,
                speciesName: "Mystery fish",
                method: "Hand lining"));
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var preferences = QuietAnglerPreferences(SamplePreferences(), SampleCatalogue());
        var synchroniser = QuietSynchroniser();
        await using var context = CreateContext(
            store,
            synchroniser: synchroniser,
            anglerPreferences: preferences);
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(page => page.CatchId, EditedCatchId));
        cut.WaitForAssertion(() =>
        {
            cut.Find("#catch-edit-method-Handlining").ClassList.Should().Contain("mud-chip-filled");
            cut.Find("#catch-edit-species-Mysteryfish").ClassList.Should().Contain("mud-chip-filled");
        });

        // Act
        await cut.Find("#catch-edit-save").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-edit-saved").Should().NotBeNull());
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Method == "Hand lining"
                && catchRecord.SpeciesName == "Mystery fish"
                && catchRecord.MetadataSyncStatus == SyncStatus.Synchronised),
            Arg.Any<CancellationToken>());
        await synchroniser.DidNotReceive().SynchronisePendingAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldApplyTheMethodChosenFromTheFullCatalogue()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var trottingMethodId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003");
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, EditedCatchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(EditedCatchId, method: "Fly"));
        var sampleCatalogue = SampleCatalogue();
        var catalogue = new FishingCatalogueDto(
            [.. sampleCatalogue.Methods, new FishingMethodDto(trottingMethodId, "Trotting", "Trotting")],
            sampleCatalogue.AllSpecies);
        var preferences = QuietAnglerPreferences(SamplePreferences(), catalogue);
        var modalService = Substitute.For<IModalService>();
        modalService
            .ShowAsync<CataloguePickerModal, CataloguePickerModalModel, CataloguePickerModalResult>(
                Arg.Any<CataloguePickerModalModel>(),
                Arg.Any<CancellationToken>())
            .Returns(new CataloguePickerModalResult(
                new CatalogueOptionModel(trottingMethodId, "Trotting", "Trotting")));
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
            cut.Find("#catch-edit-method-Trotting").ClassList.Should().Contain("mud-chip-filled"));
        await modalService.Received(1)
            .ShowAsync<CataloguePickerModal, CataloguePickerModalModel, CataloguePickerModalResult>(
                Arg.Is<CataloguePickerModalModel>(model =>
                    model.Options.Count == 3
                    && model.Options.Any(option => option.Code == "Trotting")),
                Arg.Any<CancellationToken>());
    }
}

