using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Pages.CatchEdit;
using FishingLogBook.Web.Features.Profile.Services;
using FishingLogBook.Web.Localization;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

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
        var preferenceClient = Substitute.For<IFishingPreferenceClient>();
        preferenceClient.GetCatalogueAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("offline"));
        await using var context = CreateContext(store, fishingPreferenceClient: preferenceClient);

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
        var preferenceClient = QuietFishingPreferenceClient(SamplePreferences(), SampleCatalogue());
        await using var context = CreateContext(store, fishingPreferenceClient: preferenceClient);

        // Act
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(page => page.CatchId, EditedCatchId));

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#catch-edit-method").GetAttribute("value").Should().Be("Spinning");
            cut.Find("#catch-edit-species").GetAttribute("value").Should().Be("Pike");
            cut.Find("#catch-edit-method-Spinning").ClassList.Should().Contain("mud-chip-filled");
        });
        await preferenceClient.Received(1).GetPreferencesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStillShowASavedSpeciesThatIsNotInTheShortlist()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, EditedCatchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(EditedCatchId, speciesName: "Grayling", method: "Fly"));
        var preferenceClient = QuietFishingPreferenceClient(SamplePreferences(), SampleCatalogue());
        await using var context = CreateContext(store, fishingPreferenceClient: preferenceClient);

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
        var preferenceClient = QuietFishingPreferenceClient(SamplePreferences(), SampleCatalogue());
        await using var context = CreateContext(store, fishingPreferenceClient: preferenceClient);
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
        var preferenceClient = QuietFishingPreferenceClient(SamplePreferences(), SampleCatalogue());
        var modalService = Substitute.For<IModalService>();
        modalService
            .ShowAsync<CataloguePickerModal, CataloguePickerModalModel, CataloguePickerModalResult>(
                Arg.Any<CataloguePickerModalModel>(),
                Arg.Any<CancellationToken>())
            .Returns(new CataloguePickerModalResult(
                new CatalogueOptionModel(SpinningMethodId, "Spinning", "Spinning")));
        await using var context = CreateContext(
            store,
            fishingPreferenceClient: preferenceClient,
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
