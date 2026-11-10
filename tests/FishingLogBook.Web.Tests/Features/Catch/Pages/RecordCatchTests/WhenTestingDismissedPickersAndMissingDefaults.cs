using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Pages.RecordCatch;
using FishingLogBook.Web.Localization;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.RecordCatchTests;

public class WhenTestingDismissedPickersAndMissingDefaults : BaseRecordCatchTest
{
    [Fact]
    public async Task ItShouldKeepTheSelectedMethodAndSpeciesWhenTheFullCataloguePickersAreDismissed()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        var modalService = QuietModalService();
        modalService.ShowAsync<CataloguePickerModal, CataloguePickerModalModel, CataloguePickerModalResult>(
                Arg.Any<CataloguePickerModalModel>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<CataloguePickerModalResult?>(null));
        await using var context = CreateContext(
            store,
            anglerPreferences: QuietAnglerPreferences(SamplePreferences(), SampleCatalogue()),
            modalService: modalService);
        var cut = context.Render<RecordCatch>();
        cut.WaitForAssertion(() => cut.Find("#record-catch-method-more"));
        var method = SelectedMethod(cut);
        var species = SelectedSpecies(cut);

        // Act
        await cut.Find("#record-catch-method-more").ClickAsync();
        await cut.Find("#record-catch-species-more").ClickAsync();

        // Assert
        SelectedMethod(cut).Should().Be(method);
        SelectedSpecies(cut).Should().Be(species);
        await modalService.Received(2)
            .ShowAsync<CataloguePickerModal, CataloguePickerModalModel, CataloguePickerModalResult>(
                Arg.Any<CataloguePickerModalModel>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSelectNoMethodWhenNoPreferredMethodIsMarkedAsTheDefault()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var preferences = new FishingPreferencesDto(
        [
            new FishingMethodPreferenceDto(
                FlyMethodId,
                "Fly",
                "Fly",
                false,
                [new FishingSpeciesPreferenceDto(BrownTroutSpeciesId, "BrownTrout", "Brown Trout", true)])
        ]);
        await using var context = CreateContext(
            Substitute.For<ICatchStore>(),
            anglerPreferences: QuietAnglerPreferences(preferences, SampleCatalogue()));

        // Act
        var cut = context.Render<RecordCatch>();
        cut.WaitForAssertion(() => cut.Find("#record-catch-method-chips"));

        // Assert
        SelectedMethod(cut).Should().BeEmpty();
        SelectedSpecies(cut).Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldSelectTheDefaultMethodWithoutASpeciesWhenItHasNoDefaultSpecies()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var preferences = new FishingPreferencesDto(
        [
            new FishingMethodPreferenceDto(
                FlyMethodId,
                "Fly",
                "Fly",
                true,
                [new FishingSpeciesPreferenceDto(BrownTroutSpeciesId, "BrownTrout", "Brown Trout", false)])
        ]);
        await using var context = CreateContext(
            Substitute.For<ICatchStore>(),
            anglerPreferences: QuietAnglerPreferences(preferences, SampleCatalogue()));

        // Act
        var cut = context.Render<RecordCatch>();
        cut.WaitForAssertion(() => cut.Find("#record-catch-method-chips"));

        // Assert
        SelectedMethod(cut).Should().Be("Fly");
        SelectedSpecies(cut).Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldOfferTheWholeCatalogueWhenThereAreNoPreferredMethods()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext(
            Substitute.For<ICatchStore>(),
            anglerPreferences: QuietAnglerPreferences(new FishingPreferencesDto([]), SampleCatalogue()));

        // Act
        var cut = context.Render<RecordCatch>();
        cut.WaitForAssertion(() => cut.Find("#record-catch-method-chips"));

        // Assert
        cut.Find("#record-catch-method-Fly").Should().NotBeNull();
        cut.Find("#record-catch-method-Spinning").Should().NotBeNull();
        SelectedMethod(cut).Should().BeEmpty();
    }
}
