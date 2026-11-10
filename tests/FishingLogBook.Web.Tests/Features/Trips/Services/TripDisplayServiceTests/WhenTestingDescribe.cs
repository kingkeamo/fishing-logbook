using System.Globalization;
using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Services;
using FishingLogBook.Web.Localization;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Services.TripDisplayServiceTests;

public class WhenTestingDescribe
{
    private static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset StartedOn = DateTimeOffset.Parse("2026-08-26T05:32:00Z");

    private static ITimeService TimeReturning(string value)
    {
        var time = Substitute.For<ITimeService>();
        time.ToDateTimeLocalValueAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(value);
        return time;
    }

    private static TripModel Trip(DateTimeOffset startedOn, DateTimeOffset? endedOn = null)
    {
        return new TripModel(
            TripId,
            OwnerUserId,
            endedOn is null ? TripConstants.Active : TripConstants.Completed,
            startedOn,
            endedOn);
    }

    [Fact]
    public async Task ItShouldLeaveTheLabelsEmptyWhenTheBrowserReturnsAnUnreadableTime()
    {
        // Arrange
        var time = TimeReturning("garbage");
        var sut = new TripDisplayService(time);

        // Act
        var display = await sut.DescribeAsync(Trip(StartedOn, StartedOn.AddHours(2)), CancellationToken.None);

        // Assert
        display.StartedDate.Should().BeNull();
        display.StartedTime.Should().BeNull();
        display.EndedTime.Should().BeNull();
        display.Elapsed.Should().Be(TimeSpan.FromHours(2));
        await time.Received(2).ToDateTimeLocalValueAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldDescribeTheStartAndEndOfACompletedTripInLocalTime()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var time = Substitute.For<ITimeService>();
        time.ToDateTimeLocalValueAsync(StartedOn, Arg.Any<CancellationToken>()).Returns("2026-08-26T09:32");
        time.ToDateTimeLocalValueAsync(StartedOn.AddHours(3), Arg.Any<CancellationToken>()).Returns("2026-08-26T12:32");
        var sut = new TripDisplayService(time);

        // Act
        var display = await sut.DescribeAsync(Trip(StartedOn, StartedOn.AddHours(3)), CancellationToken.None);

        // Assert
        display.StartedDate.Should().Be(new DateTime(2026, 8, 26).ToString("d MMM yyyy", CultureInfo.CurrentCulture));
        display.StartedTime.Should().Be(new DateTime(2026, 8, 26, 9, 32, 0).ToString("t", CultureInfo.CurrentCulture));
        display.EndedTime.Should().Be(new DateTime(2026, 8, 26, 12, 32, 0).ToString("t", CultureInfo.CurrentCulture));
        display.Elapsed.Should().Be(TimeSpan.FromHours(3));
    }

    [Fact]
    public async Task ItShouldNotReportAnElapsedTimeForATripThatStartsInTheFuture()
    {
        // Arrange
        var sut = new TripDisplayService(TimeReturning("2026-08-26T09:32"));

        // Act
        var display = await sut.DescribeAsync(Trip(DateTimeOffset.UtcNow.AddDays(1)), CancellationToken.None);

        // Assert
        display.Elapsed.Should().BeNull();
        display.EndedTime.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldDescribeAnInstantAsDateAndTime()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var sut = new TripDisplayService(TimeReturning("2026-08-26T09:32"));

        // Act
        var label = await sut.DescribeInstantAsync(StartedOn, CancellationToken.None);

        // Assert
        var local = new DateTime(2026, 8, 26, 9, 32, 0);
        label.Should().Be(
            $"{local.ToString("d MMM yyyy", CultureInfo.CurrentCulture)} · {local.ToString("t", CultureInfo.CurrentCulture)}");
    }

    [Fact]
    public async Task ItShouldDescribeNoInstantWhenTheBrowserReturnsAnUnreadableTime()
    {
        // Arrange
        var sut = new TripDisplayService(TimeReturning(string.Empty));

        // Act
        var label = await sut.DescribeInstantAsync(StartedOn, CancellationToken.None);

        // Assert
        label.Should().BeNull();
    }
}
