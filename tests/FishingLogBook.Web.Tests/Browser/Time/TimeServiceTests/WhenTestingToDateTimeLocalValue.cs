using AwesomeAssertions;
using FishingLogBook.Web.Browser.Time;

namespace FishingLogBook.Web.Tests.Browser.Time.TimeServiceTests;

public class WhenTestingToDateTimeLocalValue : BaseTimeServiceTest
{
    [Fact]
    public async Task ItShouldSendTheUtcIsoInstantToTheBrowser()
    {
        // Arrange
        var js = new FakeTimeJsRuntime();
        var sut = new TimeService(js);
        var instant = DateTimeOffset.Parse("2026-08-17T10:00:00Z");

        // Act
        var localValue = await sut.ToDateTimeLocalValueAsync(instant, CancellationToken.None);

        // Assert
        localValue.Should().Be("2026-08-17T14:00");
        js.ImportPaths.Should().Equal("./js/time.js");
        js.Invocations.Should().Equal("toDateTimeLocalValue");
        js.LastUtcIso.Should().Be("2026-08-17T10:00:00.000Z");
    }

    [Fact]
    public async Task ItShouldReturnEmptyWhenTheBrowserValueIsMissing()
    {
        // Arrange
        var js = new FakeTimeJsRuntime
        {
            ToDateTimeLocalResult = null
        };
        var sut = new TimeService(js);

        // Act
        var localValue = await sut.ToDateTimeLocalValueAsync(
            DateTimeOffset.Parse("2026-08-17T10:00:00Z"),
            CancellationToken.None);

        // Assert
        localValue.Should().BeEmpty();
        js.Invocations.Should().Equal("toDateTimeLocalValue");
    }
}
