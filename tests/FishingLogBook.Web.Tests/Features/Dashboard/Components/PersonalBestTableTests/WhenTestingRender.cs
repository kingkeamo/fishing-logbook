using System.Globalization;
using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Measurements;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MudBlazor.Services;
using NSubstitute;
using Component = FishingLogBook.Web.Features.Dashboard.Components.PersonalBestTable.PersonalBestTable;

namespace FishingLogBook.Web.Tests.Features.Dashboard.Components.PersonalBestTableTests;

public class WhenTestingRender
{
    private readonly ITimeService _time = TestTimeService.WithOffset(TimeSpan.FromHours(4));

    private BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(_time);
        context.Services.AddSingleton<IMeasurementService, MeasurementService>();
        return context;
    }

    private static DashboardPersonalBestDto Row(DateTimeOffset? caughtOn)
    {
        return new DashboardPersonalBestDto { CatchId = Guid.NewGuid(), Weight = 3, SpeciesName = "Pike", CaughtOn = caughtOn };
    }

    private static string Expected(int year, int month, int day)
    {
        return new DateTime(year, month, day).ToString("d", CultureInfo.CurrentCulture);
    }

    [Fact]
    public async Task ItShouldShowNotRecordedAndNotConvertWhenTheDateIsMissing()
    {
        // Arrange
        await using var context = CreateContext();

        // Act
        var cut = context.Render<Component>(p => p.Add(c => c.Rows, [Row(null)]));

        // Assert
        cut.Find("td.caught-on").TextContent.Should().NotContain("20");
        await _time.DidNotReceive().ToDateTimeLocalValueAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("en-GB")]
    [InlineData("fr-FR")]
    public async Task ItShouldFormatTheBrowserLocalDateWithTheCurrentCultureAcrossADayBoundary(string culture)
    {
        // Arrange
        using var testCulture = TestCulture.Use(culture);
        var caughtOn = DateTimeOffset.Parse("2025-12-31T22:00Z");
        await using var context = CreateContext();

        // Act
        var cut = context.Render<Component>(p => p.Add(c => c.Rows, [Row(caughtOn)]));

        // Assert
        cut.WaitForAssertion(() => cut.Find("td.caught-on").TextContent.Should().Be(Expected(2026, 1, 1)));
        await _time.Received(1).ToDateTimeLocalValueAsync(caughtOn, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a date")]
    [InlineData("2026-01-01")]
    [InlineData("2026-13-45T10:00")]
    public async Task ItShouldShowNotRecordedWhenTheConvertedValueIsUnusable(string converted)
    {
        // Arrange
        var caughtOn = DateTimeOffset.Parse("2026-01-01T10:00Z");
        _time.ToDateTimeLocalValueAsync(caughtOn, Arg.Any<CancellationToken>()).Returns(converted);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<Component>(p => p.Add(c => c.Rows, [Row(caughtOn)]));

        // Assert
        cut.Find("td.caught-on").TextContent.Should().NotContain("2026");
        await _time.Received(1).ToDateTimeLocalValueAsync(caughtOn, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStillRenderTheOtherRowsWhenOneDateConversionFails()
    {
        // Arrange
        var failing = DateTimeOffset.Parse("2026-01-01T10:00Z");
        var working = DateTimeOffset.Parse("2026-06-01T10:00Z");
        _time.ToDateTimeLocalValueAsync(failing, Arg.Any<CancellationToken>())
            .Returns<Task<string>>(_ => throw new JSException("interop failed"));
        var rows = new[] { Row(failing), Row(working) };
        await using var context = CreateContext();

        // Act
        var cut = context.Render<Component>(p => p.Add(c => c.Rows, rows));

        // Assert
        cut.WaitForAssertion(() => cut.FindAll("td.caught-on")[1].TextContent.Should().Be(Expected(2026, 6, 1)));
        cut.FindAll("tbody tr").Should().HaveCount(2);
        cut.FindAll("td.caught-on")[0].TextContent.Should().NotContain("2026");
        await _time.Received(1).ToDateTimeLocalValueAsync(failing, Arg.Any<CancellationToken>());
        await _time.Received(1).ToDateTimeLocalValueAsync(working, Arg.Any<CancellationToken>());
    }
}
