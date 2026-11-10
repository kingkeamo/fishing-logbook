using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Photographs.Enums;
using FishingLogBook.Shared.Photographs.Models;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using PhotographDetailsComponent =
    FishingLogBook.Web.Features.Photographs.Components.PhotographDetails.PhotographDetails;

namespace FishingLogBook.Web.Tests.Features.Photographs.Components.PhotographDetailsTests;

public class WhenTestingPartialMetadata
{
    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        return context;
    }

    private static IRenderedComponent<PhotographDetailsComponent> Render(
        BunitContext context,
        PhotographMetadataModel? metadata,
        string? capturedOnLocal,
        List<int> applied)
    {
        return context.Render<PhotographDetailsComponent>(parameters => parameters
            .Add(details => details.IdPrefix, "catch")
            .Add(details => details.Metadata, metadata)
            .Add(details => details.CapturedOnLocal, capturedOnLocal)
            .Add(details => details.ShowUseDetails, true)
            .Add(details => details.OnUseDetails, EventCallback.Factory.Create(new object(), () => applied.Add(1))));
    }

    [Fact]
    public async Task ItShouldOfferTheDetailsWhenOnlyTheLocationIsKnown()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var applied = new List<int>();
        var cut = Render(context, new PhotographMetadataModel(null, 53.2707, -9.0568), null, applied);

        // Act
        await cut.Find("#catch-photo-use-details").ClickAsync();

        // Assert
        cut.Find("#catch-photo-current-date").TextContent.Should().Contain("No photo date available");
        cut.Find("#catch-photo-current-location").TextContent.Should().NotContain("No photo location available");
        cut.Find("#catch-photo-use-details").HasAttribute("disabled").Should().BeFalse();
        applied.Should().HaveCount(1);
    }

    [Fact]
    public async Task ItShouldOfferTheDetailsWhenOnlyTheDateIsKnown()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var applied = new List<int>();
        var metadata = new PhotographMetadataModel(
            DateTimeOffset.Parse("2025-06-14T06:32:10Z"),
            null,
            null,
            PhotographCapturedOnSourceEnum.ExifOriginal);
        var cut = Render(context, metadata, "2025-06-14T06:32", applied);

        // Act
        await cut.Find("#catch-photo-use-details").ClickAsync();

        // Assert
        cut.Find("#catch-photo-current-location").TextContent.Should().Contain("No photo location available");
        cut.Find("#catch-photo-current-date").TextContent.Should().Contain("2025");
        applied.Should().HaveCount(1);
    }

    [Fact]
    public async Task ItShouldShowAnUnparsableLocalDateExactlyAsItWasGiven()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var cut = Render(context, PhotographMetadataModel.Empty, "yesterday afternoon", []);

        // Act
        var label = cut.Find("#catch-photo-current-date").TextContent;

        // Assert
        label.Should().Contain("yesterday afternoon");
    }

    [Fact]
    public async Task ItShouldTreatMissingMetadataAsUnavailableAndDisableTheAction()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var applied = new List<int>();
        var cut = Render(context, null, null, applied);

        // Act
        await cut.Find("#catch-photo-use-details").ClickAsync();

        // Assert
        cut.Find("#catch-photo-current-location").TextContent.Should().Contain("No photo location available");
        cut.Find("#catch-photo-use-details").HasAttribute("disabled").Should().BeTrue();
        applied.Should().BeEmpty();
    }
}
