using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Pages.RecordCatch;
using FishingLogBook.Web.Features.Profile.Services;
using FishingLogBook.Web.Localization;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.RecordCatchTests;

public class WhenTestingChips : BaseRecordCatchTest
{
    [Fact]
    public async Task ItShouldShowAHintAndNoChipsWhenTheCatalogueIsUnavailable()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        var preferenceClient = Substitute.For<IFishingPreferenceClient>();
        preferenceClient.GetCatalogueAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("offline"));
        await using var context = CreateContext(store, fishingPreferenceClient: preferenceClient);

        // Act
        var cut = context.Render<RecordCatch>();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#record-catch-catalogue-unavailable").TextContent
            .Should().Contain("Suggestions are unavailable offline."));
        cut.Find("#record-catch-method-chips").QuerySelectorAll(".mud-chip").Should().BeEmpty();
        cut.Find("#record-catch-method").Should().NotBeNull();
        await preferenceClient.Received(1).GetCatalogueAsync(Arg.Any<CancellationToken>());
        await preferenceClient.DidNotReceive().GetPreferencesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSelectTheProfileDefaultsWhenNothingHasBeenRecordedYet()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        var preferenceClient = QuietFishingPreferenceClient(SamplePreferences(), SampleCatalogue());
        await using var context = CreateContext(store, fishingPreferenceClient: preferenceClient);

        // Act
        var cut = context.Render<RecordCatch>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#record-catch-method").GetAttribute("value").Should().Be("Fly");
            cut.Find("#record-catch-species").GetAttribute("value").Should().Be("Brown Trout");
            cut.Find("#record-catch-method-Fly").ClassList.Should().Contain("mud-chip-filled");
            cut.Find("#record-catch-species-BrownTrout").ClassList.Should().Contain("mud-chip-filled");
        });
        await preferenceClient.Received(1).GetPreferencesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReplaceTheSpeciesShortlistWhenAnotherMethodIsTapped()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        var preferenceClient = QuietFishingPreferenceClient(SamplePreferences(), SampleCatalogue());
        await using var context = CreateContext(store, fishingPreferenceClient: preferenceClient);
        var cut = context.Render<RecordCatch>();
        cut.WaitForAssertion(() => cut.Find("#record-catch-species-BrownTrout"));

        // Act
        await cut.Find("#record-catch-method-Spinning").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#record-catch-method").GetAttribute("value").Should().Be("Spinning");
            cut.Find("#record-catch-species").GetAttribute("value").Should().Be("Pike");
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
        var preferenceClient = QuietFishingPreferenceClient(SamplePreferences(), SampleCatalogue());
        var modalService = Substitute.For<IModalService>();
        modalService
            .ShowAsync<CataloguePickerModal, CataloguePickerModalModel, CataloguePickerModalResult>(
                Arg.Any<CataloguePickerModalModel>(),
                Arg.Any<CancellationToken>())
            .Returns(new CataloguePickerModalResult(
                new CatalogueOptionModel(PikeSpeciesId, "Pike", "Pike")));
        await using var context = CreateContext(
            store,
            fishingPreferenceClient: preferenceClient,
            modalService: modalService);
        var cut = context.Render<RecordCatch>();
        cut.WaitForAssertion(() => cut.Find("#record-catch-species-more"));

        // Act
        await cut.Find("#record-catch-species-more").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#record-catch-species").GetAttribute("value").Should().Be("Pike"));
        await modalService.Received(1)
            .ShowAsync<CataloguePickerModal, CataloguePickerModalModel, CataloguePickerModalResult>(
                Arg.Is<CataloguePickerModalModel>(model => model.Options.Count == 2),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepATypedMethodThatIsNotInTheShortlist()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        var preferenceClient = QuietFishingPreferenceClient(SamplePreferences(), SampleCatalogue());
        await using var context = CreateContext(store, fishingPreferenceClient: preferenceClient);
        var cut = context.Render<RecordCatch>();
        cut.WaitForAssertion(() => cut.Find("#record-catch-method-Fly"));

        // Act
        cut.Find("#record-catch-method").Input("Trotting");

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#record-catch-method-Trotting").Should().NotBeNull();
            cut.Find("#record-catch-method-Trotting").ClassList.Should().Contain("mud-chip-filled");
        });
    }

    [Fact]
    public async Task ItShouldPreserveAnExplicitlyChosenSpeciesWhenTheMethodChanges()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        var preferenceClient = QuietFishingPreferenceClient(SamplePreferences(), SampleCatalogue());
        await using var context = CreateContext(store, fishingPreferenceClient: preferenceClient);
        var cut = context.Render<RecordCatch>();
        cut.WaitForAssertion(() => cut.Find("#record-catch-species-BrownTrout"));
        cut.Find("#record-catch-species").Input("Grayling");

        // Act
        await cut.Find("#record-catch-method-Spinning").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#record-catch-method").GetAttribute("value").Should().Be("Spinning");
            cut.Find("#record-catch-species").GetAttribute("value").Should().Be("Grayling");
        });
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPreserveASpeciesTappedFromAChipWhenTheMethodChanges()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        var preferenceClient = QuietFishingPreferenceClient(SamplePreferences(), SampleCatalogue());
        await using var context = CreateContext(store, fishingPreferenceClient: preferenceClient);
        var cut = context.Render<RecordCatch>();
        cut.WaitForAssertion(() => cut.Find("#record-catch-species-BrownTrout"));
        await cut.Find("#record-catch-species-BrownTrout").ClickAsync();

        // Act
        await cut.Find("#record-catch-method-Spinning").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#record-catch-method").GetAttribute("value").Should().Be("Spinning");
            cut.Find("#record-catch-species").GetAttribute("value").Should().Be("Brown Trout");
        });
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldUseProfileDefaultsForAFreshRecordCatchNotReachedByRecordAnotherCatch()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var preferenceClient = QuietFishingPreferenceClient(SamplePreferences(), SampleCatalogue());
        await using var context = CreateContext(store, fishingPreferenceClient: preferenceClient);
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
            second.Find("#record-catch-method").GetAttribute("value").Should().Be("Fly");
            second.Find("#record-catch-species").GetAttribute("value").Should().Be("Brown Trout");
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
        var preferenceClient = QuietFishingPreferenceClient(SamplePreferences(), SampleCatalogue());
        await using var context = CreateContext(store, fishingPreferenceClient: preferenceClient);
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
            cut.Find("#record-catch-method").GetAttribute("value").Should().Be("Fly");
            cut.Find("#record-catch-species").GetAttribute("value").Should().Be("Brown Trout");
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
}
