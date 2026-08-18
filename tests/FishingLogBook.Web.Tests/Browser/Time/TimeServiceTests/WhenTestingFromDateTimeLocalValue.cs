using AwesomeAssertions;
using FishingLogBook.Web.Browser.Time;

namespace FishingLogBook.Web.Tests.Browser.Time.TimeServiceTests;

public class WhenTestingFromDateTimeLocalValue : BaseTimeServiceTest
{
    [Fact]
    public async Task ItShouldReturnNullWhenTheBrowserValueIsMissing()
    {
        // Arrange
        var js = new FakeTimeJsRuntime
        {
            FromDateTimeLocalResult = null
        };
        var sut = new TimeService(js);

        // Act
        var instant = await sut.FromDateTimeLocalValueAsync("2026-08-17T14:00", CancellationToken.None);

        // Assert
        instant.Should().BeNull();
        js.LastLocalValue.Should().Be("2026-08-17T14:00");
        js.Invocations.Should().Equal("fromDateTimeLocalValue");
    }

    [Fact]
    public async Task ItShouldReturnNullWhenTheBrowserValueIsNotAnInstant()
    {
        // Arrange
        var js = new FakeTimeJsRuntime
        {
            FromDateTimeLocalResult = "not-a-date"
        };
        var sut = new TimeService(js);

        // Act
        var instant = await sut.FromDateTimeLocalValueAsync("2026-08-17T14:00", CancellationToken.None);

        // Assert
        instant.Should().BeNull();
        js.Invocations.Should().Equal("fromDateTimeLocalValue");
    }

    [Fact]
    public async Task ItShouldReturnTheUtcInstantFromTheBrowser()
    {
        // Arrange
        var js = new FakeTimeJsRuntime
        {
            FromDateTimeLocalResult = "2026-08-17T10:00:00.000Z"
        };
        var sut = new TimeService(js);

        // Act
        var instant = await sut.FromDateTimeLocalValueAsync("2026-08-17T14:00", CancellationToken.None);

        // Assert
        instant.Should().Be(DateTimeOffset.Parse("2026-08-17T10:00:00Z"));
        instant!.Value.Offset.Should().Be(TimeSpan.Zero);
        js.ImportPaths.Should().Equal("./js/time.js");
        js.LastLocalValue.Should().Be("2026-08-17T14:00");
        js.Invocations.Should().Equal("fromDateTimeLocalValue");
    }
}
