using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Onboarding.Enums;
using FishingLogBook.Mobile.Features.Onboarding.Services;
using FishingLogBook.Mobile.IO.Enums;
using FishingLogBook.Mobile.IO.Models;
using FishingLogBook.Shared.Constants;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Onboarding.Services.DeviceLocationServiceTests;

public sealed class WhenTestingCaptureCurrent : BaseDeviceLocationServiceTest
{
    [Fact]
    public async Task ItShouldNotRequestPermissionWhenItIsAlreadyGranted()
    {
        // Arrange
        // Act
        var result = await Sut.CaptureCurrentAsync(CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(LocationCaptureOutcomeEnum.Captured);
        result.Latitude.Should().Be(53.27);
        result.Longitude.Should().Be(-9.05);
        result.AccuracyMetres.Should().Be(12.4);
        await MockGeolocation.Received(1).GetWhenInUseStatusAsync(Arg.Any<CancellationToken>());
        await MockGeolocation.DidNotReceive().RequestWhenInUseAsync(Arg.Any<CancellationToken>());
        await MockGeolocation.Received(1).GetCurrentAsync(
            TimeSpan.FromMilliseconds(DeviceLocationService.CaptureTimeoutMilliseconds),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRequestPermissionAndCaptureWhenAccessIsNotYetGranted()
    {
        // Arrange
        MockGeolocation.GetWhenInUseStatusAsync(Arg.Any<CancellationToken>())
            .Returns(DeviceLocationPermissionStatusEnum.Denied);
        MockGeolocation.RequestWhenInUseAsync(Arg.Any<CancellationToken>())
            .Returns(DeviceLocationPermissionStatusEnum.Granted);

        // Act
        var result = await Sut.CaptureCurrentAsync(CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(LocationCaptureOutcomeEnum.Captured);
        result.Latitude.Should().Be(53.27);
        result.Longitude.Should().Be(-9.05);
        await MockGeolocation.Received(1).RequestWhenInUseAsync(Arg.Any<CancellationToken>());
        await MockGeolocation.Received(1).GetCurrentAsync(
            TimeSpan.FromMilliseconds(DeviceLocationService.CaptureTimeoutMilliseconds),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(DeviceLocationPermissionStatusEnum.Denied, LocationCaptureOutcomeEnum.Denied)]
    [InlineData(DeviceLocationPermissionStatusEnum.PermanentlyDenied, LocationCaptureOutcomeEnum.PermanentlyDenied)]
    [InlineData(DeviceLocationPermissionStatusEnum.Unavailable, LocationCaptureOutcomeEnum.Unavailable)]
    public async Task ItShouldNotReadGpsWhenPermissionIsNotGranted(
        DeviceLocationPermissionStatusEnum permission,
        LocationCaptureOutcomeEnum expected)
    {
        // Arrange
        MockGeolocation.GetWhenInUseStatusAsync(Arg.Any<CancellationToken>()).Returns(permission);
        MockGeolocation.RequestWhenInUseAsync(Arg.Any<CancellationToken>()).Returns(permission);

        // Act
        var result = await Sut.CaptureCurrentAsync(CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(expected);
        result.Latitude.Should().BeNull();
        result.Longitude.Should().BeNull();
        await MockGeolocation.Received(1).RequestWhenInUseAsync(Arg.Any<CancellationToken>());
        await MockGeolocation.DidNotReceive().GetCurrentAsync(
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnUnavailableWhenGpsDoesNotProvideAFix()
    {
        // Arrange
        MockGeolocation.GetCurrentAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(DeviceGpsReadModel.Failed(DeviceGpsReadOutcomeEnum.Unavailable));

        // Act
        var result = await Sut.CaptureCurrentAsync(CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(LocationCaptureOutcomeEnum.Unavailable);
        result.Latitude.Should().BeNull();
        await MockGeolocation.Received(1).GetCurrentAsync(
            TimeSpan.FromMilliseconds(DeviceLocationService.CaptureTimeoutMilliseconds),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnUnavailableWhenCoordinatesAreInvalid()
    {
        // Arrange
        MockGeolocation.GetCurrentAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(DeviceGpsReadModel.Captured(new DeviceGpsFixModel(
                CatchLocationConstants.MaxLatitude + 1,
                0,
                8,
                DateTimeOffset.Parse("2026-10-27T12:00:00Z"))));

        // Act
        var result = await Sut.CaptureCurrentAsync(CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(LocationCaptureOutcomeEnum.Unavailable);
        result.Latitude.Should().BeNull();
        await MockGeolocation.Received(1).GetCurrentAsync(
            TimeSpan.FromMilliseconds(DeviceLocationService.CaptureTimeoutMilliseconds),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnDeniedWhenTheReadIsRejected()
    {
        // Arrange
        MockGeolocation.GetCurrentAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(DeviceGpsReadModel.Failed(DeviceGpsReadOutcomeEnum.Denied));

        // Act
        var result = await Sut.CaptureCurrentAsync(CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(LocationCaptureOutcomeEnum.Denied);
        await MockGeolocation.Received(1).GetCurrentAsync(
            TimeSpan.FromMilliseconds(DeviceLocationService.CaptureTimeoutMilliseconds),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTimeoutWhenTheReadTimesOut()
    {
        // Arrange
        MockGeolocation.GetCurrentAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(DeviceGpsReadModel.Failed(DeviceGpsReadOutcomeEnum.Timeout));

        // Act
        var result = await Sut.CaptureCurrentAsync(CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(LocationCaptureOutcomeEnum.Timeout);
        await MockGeolocation.Received(1).GetCurrentAsync(
            TimeSpan.FromMilliseconds(DeviceLocationService.CaptureTimeoutMilliseconds),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldTreatAnUnexpectedCancellationAsTimeout()
    {
        // Arrange
        MockGeolocation.GetCurrentAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns<DeviceGpsReadModel>(_ => throw new OperationCanceledException());

        // Act
        var result = await Sut.CaptureCurrentAsync(CancellationToken.None);

        // Assert
        result.Outcome.Should().Be(LocationCaptureOutcomeEnum.Timeout);
        await MockGeolocation.Received(1).GetCurrentAsync(
            TimeSpan.FromMilliseconds(DeviceLocationService.CaptureTimeoutMilliseconds),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRethrowWhenTheCallerCancels()
    {
        // Arrange
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        // Act
        var act = async () => await Sut.CaptureCurrentAsync(cancelled.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        await MockGeolocation.DidNotReceive().GetWhenInUseStatusAsync(Arg.Any<CancellationToken>());
        await MockGeolocation.DidNotReceive().GetCurrentAsync(
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }
}
