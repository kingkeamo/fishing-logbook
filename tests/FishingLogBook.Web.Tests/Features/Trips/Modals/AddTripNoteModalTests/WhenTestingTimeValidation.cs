using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Enums;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Services;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.TestSupport;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.Trips.Modals.AddTripNoteModalTests;

public class WhenTestingTimeValidation : BaseAddTripNoteModalTest
{
    [Fact]
    public async Task ItShouldLogAndRequireAValidTimeWhenTheBrowserCannotFormatTheTripWindow()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var logging = Substitute.For<ILoggingService>();
        var time = Substitute.For<ITimeService>();
        time.ToDateTimeLocalValueAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("no js"));
        var writer = WriterThatSaves();
        await using var context = CreateContext(writer, logging, time);

        // Act
        var (cut, _) = await ShowModalAsync(context, endedOn: EndedOn);

        // Assert
        cut.Find("#trip-note-recorded-on-invalid").TextContent.Should().Contain("valid date and time");
        await logging.Received(2).LogErrorAsync(
            "reading a trip note time",
            Arg.Is<Exception>(exception => exception.Message == "no js"),
            CancellationToken.None);
        await writer.DidNotReceive().AddAsync(
            Arg.Any<TripNoteDraftModel>(),
            Arg.Any<TripStorageEnum>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldLogAndRequireAValidTimeWhenTheBrowserCannotParseTheChosenTime()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var logging = Substitute.For<ILoggingService>();
        var time = TestTimeService.WithOffset(TimeSpan.Zero);
        time.FromDateTimeLocalValueAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("no parse"));
        await using var context = CreateContext(WriterThatSaves(), logging, time);

        // Act
        var (cut, _) = await ShowModalAsync(context, endedOn: EndedOn);

        // Assert
        cut.Find("#trip-note-recorded-on-invalid").TextContent.Should().Contain("valid date and time");
        await logging.Received(1).LogErrorAsync(
            "reading a trip note time",
            Arg.Is<Exception>(exception => exception.Message == "no parse"),
            CancellationToken.None);
    }

    [Fact]
    public async Task ItShouldRequireAValidTimeWhenTheBrowserReturnsNoInstant()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var logging = Substitute.For<ILoggingService>();
        var time = TestTimeService.WithOffset(TimeSpan.Zero);
        time.FromDateTimeLocalValueAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((DateTimeOffset?)null);
        await using var context = CreateContext(WriterThatSaves(), logging, time);

        // Act
        var (cut, _) = await ShowModalAsync(context, endedOn: EndedOn);

        // Assert
        cut.Find("#trip-note-recorded-on-invalid").TextContent.Should().Contain("valid date and time");
        await logging.DidNotReceive().LogErrorAsync(
            Arg.Any<string>(),
            Arg.Any<Exception>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRequireAValidTimeWhenTheDateIsCleared()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var (cut, _) = await ShowModalAsync(context, endedOn: EndedOn);

        // Act
        cut.Find("#trip-note-date").Input(string.Empty);

        // Assert
        cut.Find("#trip-note-recorded-on-invalid").TextContent.Should().Contain("valid date and time");
    }

    [Fact]
    public async Task ItShouldRequireAValidTimeWhenTheTimeIsCleared()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var (cut, _) = await ShowModalAsync(context, endedOn: EndedOn);

        // Act
        cut.Find("#trip-note-time").Input(string.Empty);

        // Assert
        cut.Find("#trip-note-recorded-on-invalid").TextContent.Should().Contain("valid date and time");
    }

    [Fact]
    public async Task ItShouldRejectATimeBeforeTheTripStarted()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var (cut, _) = await ShowModalAsync(context, endedOn: EndedOn);

        // Act
        cut.Find("#trip-note-time").Input("06:00");

        // Assert
        cut.Find("#trip-note-recorded-on-invalid").TextContent.Should().Contain("before the trip started");
    }

    [Fact]
    public async Task ItShouldRejectATimeAfterTheTripFinished()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var (cut, _) = await ShowModalAsync(context, endedOn: EndedOn);

        // Act
        cut.Find("#trip-note-time").Input("17:00");

        // Assert
        cut.Find("#trip-note-recorded-on-invalid").TextContent.Should().Contain("after the trip finished");
    }

    [Fact]
    public async Task ItShouldDefaultToTheTripStartWhenTheTripHasNotYetStarted()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var startsOn = DateTimeOffset.UtcNow.AddDays(2);
        await using var context = CreateContext();

        // Act
        var (cut, _) = await ShowModalAsync(context, startedOn: startsOn);

        // Assert
        DateValue(cut).Should().Be(TestTimeService.ToDateTimeLocal(startsOn, TimeSpan.Zero)[..10]);
        TimeValue(cut).Should().Be(TestTimeService.ToDateTimeLocal(startsOn, TimeSpan.Zero)[11..16]);
    }
}
