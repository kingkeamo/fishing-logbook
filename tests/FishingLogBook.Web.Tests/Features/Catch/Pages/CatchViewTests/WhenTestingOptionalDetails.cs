using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Features.Catch.Clients;
using FishingLogBook.Web.Features.Catch.Pages.CatchView;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Localization;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.CatchViewTests;

public class WhenTestingOptionalDetails : BaseCatchViewTest
{
    private static IRenderedComponent<CatchView> RenderPage(BunitContext context)
    {
        return context.Render<CatchView>(parameters => parameters.Add(page => page.CatchId, CatchId));
    }

    [Fact]
    public async Task ItShouldOmitEveryOptionalDetailThatWasNotRecorded()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var client = ClientReturning(ViewDto(
            speciesName: " ",
            weight: null,
            length: null,
            method: null,
            baitOrLure: " ",
            notes: null,
            anglerName: null,
            recordedByName: null,
            photographs: []));
        await using var context = CreateContext(client);

        // Act
        var cut = RenderPage(context);

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-view-details").Should().NotBeNull());
        cut.Find("#catch-view-species").TextContent.Should().NotBeNullOrWhiteSpace();
        cut.FindAll("#catch-view-weight").Should().BeEmpty();
        cut.FindAll("#catch-view-length").Should().BeEmpty();
        cut.FindAll("#catch-view-method").Should().BeEmpty();
        cut.FindAll("#catch-view-bait").Should().BeEmpty();
        cut.FindAll("#catch-view-notes").Should().BeEmpty();
        cut.FindAll("#catch-view-caught-by").Should().BeEmpty();
        cut.FindAll("#catch-view-recorded-by").Should().BeEmpty();
        cut.FindAll("#catch-view-location").Should().BeEmpty();
        await client.Received(1).GetAsync(CatchId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowWeightAndLengthInThePreferredImperialUnits()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var client = ClientReturning(ViewDto(weight: 1.02m, length: 48m));
        await using var context = CreateContext(
            client,
            preferences: QuietPreferences(WeightUnitEnum.Lb, LengthUnitEnum.In));

        // Act
        var cut = RenderPage(context);

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-view-weight").Should().NotBeNull());
        cut.Find("#catch-view-weight").TextContent.Should().Contain("lb");
        cut.Find("#catch-view-length").TextContent.Should().Contain("in");
    }

    [Fact]
    public async Task ItShouldNotRepeatTheRecorderWhenTheAnglerRecordedTheirOwnCatch()
    {
        // Arrange
        var client = Substitute.For<ICatchClient>();
        client.GetAsync(CatchId, Arg.Any<CancellationToken>()).Returns(new CatchViewDto(CatchId, OwnerUserId, CaughtOn)
        {
            AnglerName = "Mark",
            RecordedByUserId = OwnerUserId,
            RecordedByName = "Mark",
            SpeciesName = "Pike"
        });
        await using var context = CreateContext(client);

        // Act
        var cut = RenderPage(context);

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-view-caught-by").Should().NotBeNull());
        cut.FindAll("#catch-view-recorded-by").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldNameTheVenueWhenNoCoordinatesAreExposed()
    {
        // Arrange
        var client = ClientReturning(ViewDto(location: new CatchLocationExposureDto
        {
            Visibility = "Public",
            Mode = "Venue",
            FishingVenueName = "Lough Corrib"
        }));
        await using var context = CreateContext(client);

        // Act
        var cut = RenderPage(context);

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-view-location").TextContent.Should().Contain("Lough Corrib"));
    }

    [Fact]
    public async Task ItShouldOmitTheLocationWhenNeitherCoordinatesNorAVenueAreExposed()
    {
        // Arrange
        var client = ClientReturning(ViewDto(location: new CatchLocationExposureDto
        {
            Visibility = "Private",
            Mode = "Hidden",
            FishingVenueName = " "
        }));
        await using var context = CreateContext(client);

        // Act
        var cut = RenderPage(context);

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-view-details").Should().NotBeNull());
        cut.FindAll("#catch-view-location").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldFallBackToTheInstantWhenTheLocalTimeCannotBeParsed()
    {
        // Arrange
        var time = Substitute.For<ITimeService>();
        time.ToDateTimeLocalValueAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns("not a date");
        await using var context = CreateContext(ClientReturning(ViewDto()), time: time);

        // Act
        var cut = RenderPage(context);

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-view-caught-on").TextContent.Should().Contain("2026"));
        await time.Received(1).ToDateTimeLocalValueAsync(CaughtOn, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldFallBackToTheInstantAndLogWhenTheTimeServiceFails()
    {
        // Arrange
        var time = Substitute.For<ITimeService>();
        time.ToDateTimeLocalValueAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<string>(new InvalidOperationException("js unavailable")));
        var logging = QuietLogging();
        await using var context = CreateContext(ClientReturning(ViewDto()), time: time, logging: logging);

        // Act
        var cut = RenderPage(context);

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-view-caught-on").TextContent.Should().Contain("2026"));
        await time.Received(1).ToDateTimeLocalValueAsync(CaughtOn, Arg.Any<CancellationToken>());
        await logging.Received(1).LogErrorAsync(
            "reading a catch view time",
            Arg.Is<Exception>(exception => exception.Message == "js unavailable"),
            CancellationToken.None);
    }
}
