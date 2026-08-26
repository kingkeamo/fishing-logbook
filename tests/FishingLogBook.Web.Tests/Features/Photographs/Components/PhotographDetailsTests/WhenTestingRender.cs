using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Photographs.Enums;
using FishingLogBook.Web.Features.Photographs.Models;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using PhotographDetailsComponent =
    FishingLogBook.Web.Features.Photographs.Components.PhotographDetails.PhotographDetails;

namespace FishingLogBook.Web.Tests.Features.Photographs.Components.PhotographDetailsTests;

public class WhenTestingRender
{
    private static readonly Guid PhotographId = Guid.Parse("22222222-0000-0000-0000-000000000001");

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        return context;
    }

    [Fact]
    public async Task ItShouldStateWhatIsUnknownAndDisableTheActionWhenNothingIsAvailable()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var applied = 0;

        // Act
        var cut = context.Render<PhotographDetailsComponent>(parameters => parameters
            .Add(details => details.IdPrefix, "catch")
            .Add(details => details.PhotographId, PhotographId)
            .Add(details => details.Metadata, PhotographMetadataModel.Empty)
            .Add(details => details.ShowUseDetails, true)
            .Add(details => details.OnUseDetails, EventCallback.Factory.Create(this, () => applied++)));

        // Assert
        cut.Find("#catch-photo-current-date").TextContent.Should().Contain("No photo date available");
        cut.Find("#catch-photo-current-date").GetAttribute("data-captured-on").Should().BeEmpty();
        cut.Find("#catch-photo-current-location").TextContent.Should()
            .Contain("No photo location available");
        cut.Find("#catch-photo-use-details").HasAttribute("disabled").Should().BeTrue();
        await cut.Find("#catch-photo-use-details").ClickAsync();
        applied.Should().Be(0);
    }

    [Fact]
    public async Task ItShouldOmitTheActionWhenTheConsumerDoesNotOfferIt()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<PhotographDetailsComponent>(parameters => parameters
            .Add(details => details.IdPrefix, "catch-edit")
            .Add(details => details.PhotographId, PhotographId)
            .Add(details => details.Metadata, new PhotographMetadataModel(
                DateTimeOffset.Parse("2025-06-14T06:32:10Z"),
                null,
                null,
                PhotographCapturedOnSourceEnum.ExifOriginal))
            .Add(details => details.CapturedOnLocal, "2025-06-14T06:32")
            .Add(details => details.ShowUseDetails, false));

        // Assert
        cut.FindAll("#catch-edit-photo-use-details").Should().BeEmpty();
        cut.Find("#catch-edit-photo-current-date").GetAttribute("data-captured-on").Should()
            .Be("2025-06-14T06:32");
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ItShouldDescribeAvailabilityInTextAndRaiseTheNeutralAction()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var applied = 0;

        // Act
        var cut = context.Render<PhotographDetailsComponent>(parameters => parameters
            .Add(details => details.IdPrefix, "catch")
            .Add(details => details.PhotographId, PhotographId)
            .Add(details => details.Metadata, new PhotographMetadataModel(
                DateTimeOffset.Parse("2025-06-14T06:32:10Z"),
                53.2707,
                -9.0568,
                PhotographCapturedOnSourceEnum.ExifOriginal))
            .Add(details => details.CapturedOnLocal, "2025-06-14T06:32")
            .Add(details => details.IsChosen, true)
            .Add(details => details.ShowUseDetails, true)
            .Add(details => details.OnUseDetails, EventCallback.Factory.Create(this, () => applied++)));

        // Assert
        cut.Find("#catch-photo-current-metadata").GetAttribute("data-photograph-id").Should()
            .Be(PhotographId.ToString());
        cut.Find("#catch-photo-current-date").TextContent.Should().Contain("14/06/2025");
        cut.Find("#catch-photo-current-location").TextContent.Should().Contain("GPS location available");
        cut.Find("#catch-photo-current-location").TextContent.Should().NotContain("53.2707");
        cut.Find("#catch-photo-use-details").HasAttribute("disabled").Should().BeFalse();
        await cut.Find("#catch-photo-use-details").ClickAsync();
        applied.Should().Be(1);
    }
}
