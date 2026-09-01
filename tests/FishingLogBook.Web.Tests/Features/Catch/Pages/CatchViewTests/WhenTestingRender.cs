using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Catch.Clients;
using FishingLogBook.Web.Features.Catch.Pages.CatchView;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.CatchViewTests;

public class WhenTestingRender : BaseCatchViewTest
{
    [Fact]
    public void ItShouldRequireAnAuthenticatedUser()
    {
        // Arrange
        // Act
        var authorize = typeof(CatchView)
            .GetCustomAttributes(inherit: true)
            .OfType<AuthorizeAttribute>()
            .SingleOrDefault();

        // Assert
        authorize.Should().NotBeNull();
    }

    [Fact]
    public async Task ItShouldNotOfferEditOrSaveControls()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchClient = ClientReturning(ViewDto());
        await using var context = CreateContext(catchClient);

        // Act
        var cut = context.Render<CatchView>(parameters => parameters.Add(page => page.CatchId, CatchId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-view-details").Should().NotBeNull());
        cut.FindAll("#catch-view-save").Should().BeEmpty();
        cut.FindAll("#catch-edit-save").Should().BeEmpty();
        cut.FindAll("#catch-view-photo-remove").Should().BeEmpty();
        cut.Markup.Should().NotContain("Save details");
        cut.Markup.Should().NotContain("Edit catch");
        await catchClient.Received(1).GetAsync(CatchId, Arg.Any<CancellationToken>());
        await catchClient.DidNotReceive().UpsertAsync(Arg.Any<CatchDto>(), Arg.Any<CancellationToken>());
        await catchClient.DidNotReceive().DeletePhotographAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
        await catchClient.DidNotReceive().UpdateLocationVisibilityAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
        await catchClient.DidNotReceive().CorrectAnglerAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldOmitLocationWhenTheApiHidesIt()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchClient = ClientReturning(ViewDto(
            location: new CatchLocationExposureDto
            {
                Visibility = "Private",
                Mode = "None"
            }));
        await using var context = CreateContext(catchClient);

        // Act
        var cut = context.Render<CatchView>(parameters => parameters.Add(page => page.CatchId, CatchId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-view-details").Should().NotBeNull());
        cut.FindAll("#catch-view-location").Should().BeEmpty();
        await catchClient.Received(1).GetAsync(CatchId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRenderApproximateLocationWithoutExactCoordinates()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchClient = ClientReturning(ViewDto(
            location: new CatchLocationExposureDto
            {
                Visibility = "Approximate",
                Mode = "Approximate",
                ApproximateLatitude = 53.25,
                ApproximateLongitude = -7.75
            }));
        await using var context = CreateContext(catchClient);

        // Act
        var cut = context.Render<CatchView>(parameters => parameters.Add(page => page.CatchId, CatchId));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-view-location").TextContent.Should().Contain("53.25").And.Contain("-7.75"));
        await catchClient.Received(1).GetAsync(CatchId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRenderCatchDetails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchClient = ClientReturning(ViewDto(
            location: new CatchLocationExposureDto
            {
                Visibility = "Public",
                Mode = "Exact",
                Latitude = 53.5,
                Longitude = -7.9
            }));
        await using var context = CreateContext(catchClient);

        // Act
        var cut = context.Render<CatchView>(parameters => parameters.Add(page => page.CatchId, CatchId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-view-details").Should().NotBeNull());
        cut.Find("#catch-view-title").TextContent.Should().Contain("Catch");
        cut.Find("#catch-view-species").TextContent.Should().Contain("Brown Trout");
        cut.Find("#catch-view-caught-on").TextContent.Should().Contain("17/08/2026").And.Contain("08:00");
        cut.Find("#catch-view-weight").TextContent.Should().Contain("1.02").And.Contain("kg");
        cut.Find("#catch-view-length").TextContent.Should().Contain("48").And.Contain("cm");
        cut.Find("#catch-view-method").TextContent.Should().Contain("Fly");
        cut.Find("#catch-view-bait").TextContent.Should().Contain("Mayfly");
        cut.Find("#catch-view-notes").TextContent.Should().Contain("Took on the drift.");
        cut.Find("#catch-view-caught-by").TextContent.Should().Contain("Mark");
        cut.Find("#catch-view-recorded-by").TextContent.Should().Contain("Eamonn");
        cut.Find("#catch-view-location").TextContent.Should().Contain("53.5").And.Contain("-7.9");
        cut.Find("#catch-view-photo").GetAttribute("src").Should().Be("https://storage.test/catch.jpg");
        await catchClient.Received(1).GetAsync(CatchId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowFrenchCopy()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        var catchClient = ClientReturning(ViewDto());
        await using var context = CreateContext(catchClient);

        // Act
        var cut = context.Render<CatchView>(parameters => parameters.Add(page => page.CatchId, CatchId));

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#catch-view-title").TextContent.Should().Contain("Prise");
            cut.Find("#catch-view-species").TextContent.Should().Contain("Brown Trout");
        });
        cut.Markup.Should().Contain("Espèce");
        cut.Markup.Should().Contain("Attrapé par");
        await catchClient.Received(1).GetAsync(CatchId, Arg.Any<CancellationToken>());
    }
}
