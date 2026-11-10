using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Photographs.Mappers;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Photographs.Mappers.DevicePhotographLocalTimeMapperTests;

public class WhenTestingFromDateTimeLocalValue
{
    private readonly TimeProvider _mockTimeProvider = Substitute.ForPartsOf<TimeProvider>();
    private readonly DevicePhotographLocalTimeMapper _sut;

    public WhenTestingFromDateTimeLocalValue()
    {
        _sut = new DevicePhotographLocalTimeMapper(_mockTimeProvider);
    }

    [Fact]
    public async Task ItShouldPropagateCancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var act = () => _sut.FromDateTimeLocalValueAsync("2026-08-17T09:30:00", cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a date")]
    [InlineData("2026-08-17")]
    [InlineData("2026-08-17T09:30")]
    [InlineData("2026-08-17 09:30:00")]
    [InlineData("2026-13-40T09:30:00")]
    public async Task ItShouldReturnNothingForAValueThatIsNotADateTimeLocalValue(string value)
    {
        // Arrange
        // Act
        var result = await _sut.FromDateTimeLocalValueAsync(value, CancellationToken.None);

        // Assert
        result.Should().BeNull();
        _ = _mockTimeProvider.DidNotReceive().LocalTimeZone;
    }

    [Fact]
    public async Task ItShouldInterpretTheWallClockInTheDeviceTimeZoneRatherThanAsUtc()
    {
        // Arrange
        var zone = TimeZoneInfo.CreateCustomTimeZone("Plus2", TimeSpan.FromHours(2), "Plus2", "Plus2");
        _mockTimeProvider.LocalTimeZone.Returns(zone);

        // Act
        var result = await _sut.FromDateTimeLocalValueAsync("2026-08-17T09:30:00", CancellationToken.None);

        // Assert
        result.Should().Be(new DateTimeOffset(2026, 8, 17, 9, 30, 0, TimeSpan.FromHours(2)));
        result!.Value.UtcDateTime.Should().Be(new DateTime(2026, 8, 17, 7, 30, 0, DateTimeKind.Utc));
        _ = _mockTimeProvider.Received(1).LocalTimeZone;
    }

    [Fact]
    public async Task ItShouldUseTheOffsetThatAppliesOnTheGivenDateForAZoneWithDaylightSaving()
    {
        // Arrange
        _mockTimeProvider.LocalTimeZone.Returns(
            TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "GMT Standard Time" : "Europe/Dublin"));

        // Act
        var winter = await _sut.FromDateTimeLocalValueAsync("2026-01-15T12:00:00", CancellationToken.None);
        var summer = await _sut.FromDateTimeLocalValueAsync("2026-07-15T12:00:00", CancellationToken.None);

        // Assert
        winter!.Value.Offset.Should().Be(TimeSpan.Zero);
        summer!.Value.Offset.Should().Be(TimeSpan.FromHours(1));
        _ = _mockTimeProvider.Received(2).LocalTimeZone;
    }
}
