using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Pages.RecordCatch;
using FishingLogBook.Web.Localization;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.RecordCatchTests;

public class WhenTestingChips : BaseRecordCatchTest
{
    private static readonly Guid TrottingMethodId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003");
    private static readonly Guid GraylingSpeciesId = Guid.Parse("cccccccc-0000-0000-0000-000000000003");

    [Fact]
    public async Task ItShouldShowAHintAndNoChipsWhenTheCatalogueIsUnavailable()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        var preferences = QuietAnglerPreferences();
        await using var context = CreateContext(store, anglerPreferences: preferences);

        // Act
        var cut = context.Render<RecordCatch>();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#record-catch-catalogue-unavailable").TextContent
            .Should().Contain("Suggestions are unavailable offline."));
        cut.Find("#record-catch-method-chips").QuerySelectorAll(".mud-chip").Should().BeEmpty();
        cut.Find("#record-catch-species-chips").QuerySelectorAll(".mud-chip").Should().BeEmpty();
        cut.Find("#record-catch-method-more").Should().NotBeNull();
        cut.FindAll("#record-catch-method").Should().BeEmpty();
        cut.FindAll("#record-catch-species").Should().BeEmpty();
        await preferences.Received(1).GetAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldClearAnAutoDefaultedSpeciesWhenTheNewMethodHasNoDefaultSpecies()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        var storedPreferences = new FishingPreferencesDto(
        [
            new FishingMethodPreferenceDto(
                FlyMethodId,
                "Fly",
                "Fly",
                true,
                [new FishingSpeciesPreferenceDto(BrownTroutSpeciesId, "BrownTrout", "Brown Trout", true)]),
            new FishingMethodPreferenceDto(SpinningMethodId, "Spinning", "Spinning", false, [])
        ]);
        var preferences = QuietAnglerPreferences(storedPreferences, SampleCatalogue());
        await using var context = CreateContext(store, anglerPreferences: preferences);
        var cut = context.Render<RecordCatch>();
        cut.WaitForAssertion(() => SelectedSpecies(cut).Should().Be("Brown Trout"));

        // Act
        await cut.Find("#record-catch-method-Spinning").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            SelectedMethod(cut).Should().Be("Spinning");
            SelectedSpecies(cut).Should().BeEmpty();
        });
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepAMethodChosenFromTheFullCatalogueThatIsNotInTheShortlist()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        var catalogue = new FishingCatalogueDto(
            [
                new FishingMethodDto(FlyMethodId, "Fly", "Fly"),
                new FishingMethodDto(SpinningMethodId, "Spinning", "Spinning"),
                new FishingMethodDto(TrottingMethodId, "Trotting", "Trotting")
            ],
            [
                new SpeciesDto(BrownTroutSpeciesId, "BrownTrout", "Brown Trout"),
                new SpeciesDto(PikeSpeciesId, "Pike", "Pike")
            ]);
        var preferences = QuietAnglerPreferences(SamplePreferences(), catalogue);
        var modalService = QuietModalService();
        AnswerCataloguePicker(
            modalService,
            "Trotting",
            new CatalogueOptionModel(TrottingMethodId, "Trotting", "Trotting"));
        await using var context = CreateContext(
            store,
            anglerPreferences: preferences,
            modalService: modalService);
        var cut = context.Render<RecordCatch>();
        cut.WaitForAssertion(() => cut.Find("#record-catch-method-Fly"));

        // Act
        await cut.Find("#record-catch-method-more").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#record-catch-method-Trotting").ClassList.Should().Contain("mud-chip-filled");
            SelectedMethod(cut).Should().Be("Trotting");
        });
        await modalService.Received(1)
            .ShowAsync<CataloguePickerModal, CataloguePickerModalModel, CataloguePickerModalResult>(
                Arg.Is<CataloguePickerModalModel>(model => model.Options.Count == 3),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPreserveAnExplicitlyChosenSpeciesWhenTheMethodChanges()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        var catalogue = new FishingCatalogueDto(
            [
                new FishingMethodDto(FlyMethodId, "Fly", "Fly"),
                new FishingMethodDto(SpinningMethodId, "Spinning", "Spinning")
            ],
            [
                new SpeciesDto(BrownTroutSpeciesId, "BrownTrout", "Brown Trout"),
                new SpeciesDto(PikeSpeciesId, "Pike", "Pike"),
                new SpeciesDto(GraylingSpeciesId, "Grayling", "Grayling")
            ]);
        var preferences = QuietAnglerPreferences(SamplePreferences(), catalogue);
        var modalService = QuietModalService();
        AnswerCataloguePicker(
            modalService,
            "Grayling",
            new CatalogueOptionModel(GraylingSpeciesId, "Grayling", "Grayling"));
        await using var context = CreateContext(
            store,
            anglerPreferences: preferences,
            modalService: modalService);
        var cut = context.Render<RecordCatch>();
        cut.WaitForAssertion(() => cut.Find("#record-catch-species-BrownTrout"));
        await cut.Find("#record-catch-species-more").ClickAsync();
        cut.WaitForAssertion(() => SelectedSpecies(cut).Should().Be("Grayling"));

        // Act
        await cut.Find("#record-catch-method-Spinning").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            SelectedMethod(cut).Should().Be("Spinning");
            SelectedSpecies(cut).Should().Be("Grayling");
        });
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPreserveASpeciesTappedFromAChipWhenTheMethodChanges()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        var preferences = QuietAnglerPreferences(SamplePreferences(), SampleCatalogue());
        await using var context = CreateContext(store, anglerPreferences: preferences);
        var cut = context.Render<RecordCatch>();
        cut.WaitForAssertion(() => cut.Find("#record-catch-species-BrownTrout"));
        await cut.Find("#record-catch-species-BrownTrout").ClickAsync();

        // Act
        await cut.Find("#record-catch-method-Spinning").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            SelectedMethod(cut).Should().Be("Spinning");
            SelectedSpecies(cut).Should().Be("Brown Trout");
        });
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReplaceTheSpeciesShortlistWhenAnotherMethodIsTapped()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        var preferences = QuietAnglerPreferences(SamplePreferences(), SampleCatalogue());
        await using var context = CreateContext(store, anglerPreferences: preferences);
        var cut = context.Render<RecordCatch>();
        cut.WaitForAssertion(() => cut.Find("#record-catch-species-BrownTrout"));

        // Act
        await cut.Find("#record-catch-method-Spinning").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            SelectedMethod(cut).Should().Be("Spinning");
            SelectedSpecies(cut).Should().Be("Pike");
            cut.Find("#record-catch-species-Pike").Should().NotBeNull();
            cut.FindAll("#record-catch-species-BrownTrout").Should().BeEmpty();
        });
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldApplyTheChoiceMadeInTheFullCatalogue()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        var preferences = QuietAnglerPreferences(SamplePreferences(), SampleCatalogue());
        var modalService = QuietModalService();
        AnswerCataloguePicker(
            modalService,
            "Pike",
            new CatalogueOptionModel(PikeSpeciesId, "Pike", "Pike"));
        await using var context = CreateContext(
            store,
            anglerPreferences: preferences,
            modalService: modalService);
        var cut = context.Render<RecordCatch>();
        cut.WaitForAssertion(() => cut.Find("#record-catch-species-more"));

        // Act
        await cut.Find("#record-catch-species-more").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => SelectedSpecies(cut).Should().Be("Pike"));
        await modalService.Received(1)
            .ShowAsync<CataloguePickerModal, CataloguePickerModalModel, CataloguePickerModalResult>(
                Arg.Is<CataloguePickerModalModel>(model => model.Options.Count == 2),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldUseProfileDefaultsForAFreshRecordCatchNotReachedByRecordAnotherCatch()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var preferences = QuietAnglerPreferences(SamplePreferences(), SampleCatalogue());
        await using var context = CreateContext(store, anglerPreferences: preferences);
        var first = context.Render<RecordCatch>();
        first.WaitForAssertion(() => first.Find("#record-catch-method-Fly"));
        await first.Find("#record-catch-method-Spinning").ClickAsync();
        first.FindComponents<Microsoft.AspNetCore.Components.Forms.InputFile>()[0]
            .UploadFiles(JpegFile("catch.jpg", 0xFF, 0xD8, 0xFF));
        await first.Find("#save-catch-button").ClickAsync();
        first.WaitForAssertion(() => first.Find("#catch-saved"));

        // Act
        var second = context.Render<RecordCatch>();

        // Assert
        second.WaitForAssertion(() =>
        {
            SelectedMethod(second).Should().Be("Fly");
            SelectedSpecies(second).Should().Be("Brown Trout");
        });
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Method == "Spinning"
                && catchRecord.SpeciesName == "Pike"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldCarryMethodAndSpeciesButNoMeasurementsIntoTheNextCatch()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var preferences = QuietAnglerPreferences(SamplePreferences(), SampleCatalogue());
        await using var context = CreateContext(store, anglerPreferences: preferences);
        var cut = context.Render<RecordCatch>();
        cut.WaitForAssertion(() => cut.Find("#record-catch-method-Fly"));
        cut.FindComponents<Microsoft.AspNetCore.Components.Forms.InputFile>()[0]
            .UploadFiles(JpegFile("catch.jpg", 0xFF, 0xD8, 0xFF));
        await cut.Find("#save-catch-button").ClickAsync();
        cut.WaitForAssertion(() => cut.Find("#catch-record-another"));

        // Act
        await cut.Find("#catch-record-another").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            SelectedMethod(cut).Should().Be("Fly");
            SelectedSpecies(cut).Should().Be("Brown Trout");
            cut.Find("#save-catch-button").HasAttribute("disabled").Should().BeTrue();
            cut.FindAll("#catch-photo-carousel").Should().BeEmpty();
        });
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Method == "Fly"
                && catchRecord.SpeciesName == "Brown Trout"
                && catchRecord.Weight == null
                && catchRecord.Length == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSelectTheProfileDefaultsWhenNothingHasBeenRecordedYet()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        var preferences = QuietAnglerPreferences(SamplePreferences(), SampleCatalogue());
        await using var context = CreateContext(store, anglerPreferences: preferences);

        // Act
        var cut = context.Render<RecordCatch>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            SelectedMethod(cut).Should().Be("Fly");
            SelectedSpecies(cut).Should().Be("Brown Trout");
            cut.Find("#record-catch-method-Fly").ClassList.Should().Contain("mud-chip-filled");
            cut.Find("#record-catch-species-BrownTrout").ClassList.Should().Contain("mud-chip-filled");
        });
        await preferences.Received(1).GetAsync(Arg.Any<CancellationToken>());
    }
}
