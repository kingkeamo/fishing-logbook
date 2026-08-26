using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Pages.RecordCatch;
using FishingLogBook.Web.Features.Profile.Models;
using FishingLogBook.Web.Features.Profile.Providers;
using FishingLogBook.Web.Localization;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.RecordCatchTests;

public class WhenTestingLayout : BaseRecordCatchTest
{
    [Fact]
    public async Task ItShouldShowASpinnerUntilThePreferencesHaveLoaded()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        var loaded = new TaskCompletionSource<AnglerPreferencesModel>();
        var preferences = Substitute.For<IAnglerPreferencesProvider>();
        preferences.GetAsync(Arg.Any<CancellationToken>()).Returns(loaded.Task);
        await using var context = CreateContext(store, anglerPreferences: preferences);
        var cut = context.Render<RecordCatch>();
        cut.Find("#record-catch-loading").Should().NotBeNull();
        cut.FindAll("#record-catch-method-chips").Should().BeEmpty();
        cut.FindAll("#catch-take-photo").Should().BeEmpty();

        // Act
        await cut.InvokeAsync(() => loaded.SetResult(new AnglerPreferencesModel(
            SampleCatalogue(),
            SamplePreferences(),
            WeightUnitEnum.Kg,
            LengthUnitEnum.Cm)));

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.FindAll("#record-catch-loading").Should().BeEmpty();
            cut.Find("#record-catch-method-Fly").Should().NotBeNull();
            cut.Find("#catch-take-photo").Should().NotBeNull();
        });
        await preferences.Received(1).GetAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotRenderTheFreeTextMethodAndSpeciesFields()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        var preferences = QuietAnglerPreferences(SamplePreferences(), SampleCatalogue());
        await using var context = CreateContext(store, anglerPreferences: preferences);

        // Act
        var cut = context.Render<RecordCatch>();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#record-catch-method-Fly"));
        cut.FindAll("#record-catch-method").Should().BeEmpty();
        cut.FindAll("#record-catch-species").Should().BeEmpty();
        cut.Find("#record-catch-method-more").Should().NotBeNull();
        cut.Find("#record-catch-species-more").Should().NotBeNull();
        await preferences.Received(1).GetAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldMakeTakePhotoThePrimaryAction()
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
            cut.Find("#catch-take-photo").ClassList
                .Should().Contain("photograph-picker-action-primary");
            cut.Find("#catch-choose-photo").ClassList
                .Should().Contain("photograph-picker-action-secondary");
        });
        cut.Find("#catch-take-photo").TextContent.Trim().Should().Be("Take photo");
        cut.Find("#catch-choose-photo").TextContent.Trim().Should().Be("Choose photo");
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowACompressedHeaderWithoutASubtitle()
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
            cut.Find("#record-catch-title").TextContent.Trim().Should().Be("Record catch"));
        cut.Markup.Should().NotContain("Photograph, then save on this device");
        await preferences.Received(1).GetAsync(Arg.Any<CancellationToken>());
    }
}
