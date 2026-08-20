using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Catch.Components.CatchCard;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;

namespace FishingLogBook.Web.Tests.Features.Catch.Components.CatchCardTests;

public class WhenTestingInteractions : BaseCatchCardTest
{
    [Fact]
    public async Task ItShouldLinkTheCardBodyToTheEditPage()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.NewGuid();
        var stored = StoredCatch(catchId);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<CatchCard>(parameters => parameters
            .Add(card => card.Catch, stored)
            .Add(card => card.LocalCaughtOn, LocalToday)
            .Add(card => card.LocalToday, LocalToday));

        // Assert
        cut.Find($"#catch-card-link-{catchId:D}").GetAttribute("href")
            .Should().Be($"/catches/{catchId:D}/edit");
    }

    [Fact]
    public async Task ItShouldLinkTheEditMenuItemToTheEditPage()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.NewGuid();
        var stored = StoredCatch(catchId);
        await using var context = CreateContext();
        var (cut, popover) = RenderCard(context, parameters => parameters
            .Add(card => card.Catch, stored)
            .Add(card => card.LocalCaughtOn, LocalToday)
            .Add(card => card.LocalToday, LocalToday));

        // Act
        await cut.Find($"#catch-card-menu-{catchId:D}").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            popover.Find($"#catch-card-edit-{catchId:D}").GetAttribute("href")
                .Should().Be($"/catches/{catchId:D}/edit"));
    }

    [Fact]
    public async Task ItShouldRaiseTheLocationPrivacyCallbackWhenTheMenuItemIsClicked()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.NewGuid();
        var stored = StoredCatch(
            catchId,
            location: new FishingLogBook.Web.Features.Catch.Models.CatchLocationModel(
                53.2707,
                -9.0568,
                12,
                DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
                "DeviceGps",
                "Private",
                "1"));
        Guid? raisedCatchId = null;
        await using var context = CreateContext();
        var (cut, popover) = RenderCard(context, parameters => parameters
            .Add(card => card.Catch, stored)
            .Add(card => card.LocalCaughtOn, LocalToday)
            .Add(card => card.LocalToday, LocalToday)
            .Add(card => card.OnLocationPrivacy, EventCallback.Factory.Create<Guid>(this, id => raisedCatchId = id)));
        await cut.Find($"#catch-card-menu-{catchId:D}").ClickAsync();
        cut.WaitForAssertion(() => popover.Find($"#catch-card-location-privacy-{catchId:D}").Should().NotBeNull());

        // Act
        await popover.Find($"#catch-card-location-privacy-{catchId:D}").ClickAsync();

        // Assert
        raisedCatchId.Should().Be(catchId);
    }

    [Fact]
    public async Task ItShouldRaiseTheRetryCallbackWithTheCatchId()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.NewGuid();
        var stored = StoredCatch(catchId, syncStatus: SyncStatus.FailedToSynchronise);
        Guid? raisedCatchId = null;
        await using var context = CreateContext();

        // Act
        var cut = context.Render<CatchCard>(parameters => parameters
            .Add(card => card.Catch, stored)
            .Add(card => card.LocalCaughtOn, LocalToday)
            .Add(card => card.LocalToday, LocalToday)
            .Add(card => card.OnRetry, EventCallback.Factory.Create<Guid>(this, id => raisedCatchId = id)));
        await cut.Find($"#catch-sync-retry-{catchId:D}").ClickAsync();

        // Assert
        raisedCatchId.Should().Be(catchId);
    }

    [Fact]
    public async Task ItShouldDisableRetryWhileRetrying()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.NewGuid();
        var stored = StoredCatch(catchId, syncStatus: SyncStatus.FailedToSynchronise);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<CatchCard>(parameters => parameters
            .Add(card => card.Catch, stored)
            .Add(card => card.LocalCaughtOn, LocalToday)
            .Add(card => card.LocalToday, LocalToday)
            .Add(card => card.IsRetrying, true));

        // Assert
        cut.Find($"#catch-sync-retry-{catchId:D}").HasAttribute("disabled").Should().BeTrue();
    }
}
