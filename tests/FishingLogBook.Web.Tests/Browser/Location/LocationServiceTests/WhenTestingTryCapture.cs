using AwesomeAssertions;
using FishingLogBook.Shared.Diagnostics;
using FishingLogBook.Shared.Dtos;
using Microsoft.JSInterop;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Browser.Location.LocationServiceTests;

public class WhenTestingTryCapture : BaseLocationServiceTest
{
    private const string ValidFix =
        """{"latitude":53.3498,"longitude":-6.2603,"accuracy":12.5,"timestamp":"2026-08-17T08:00:00+00:00"}""";

    [Fact]
    public async Task ItShouldNotCaptureOnSaveWhenPermissionHasNotBeenGranted()
    {
        // Arrange
        Js.Permission = "prompt";

        // Act
        var location = await Sut.TryCaptureAsync(false, CancellationToken.None);

        // Assert
        location.Should().BeNull();
        Js.Invoked.Should().NotContain("getCurrent");
    }

    [Fact]
    public async Task ItShouldNotCaptureOnSaveWhenPermissionWasDenied()
    {
        // Arrange
        Js.Permission = "denied";

        // Act
        var location = await Sut.TryCaptureAsync(false, CancellationToken.None);

        // Assert
        location.Should().BeNull();
        Js.Invoked.Should().NotContain("getCurrent");
    }

    [Fact]
    public async Task ItShouldCaptureWhenTheUserAsksEvenBeforePermissionIsGranted()
    {
        // Arrange
        Js.Permission = "prompt";
        Js.CurrentJson = ValidFix;

        // Act
        var location = await Sut.TryCaptureAsync(true, CancellationToken.None);

        // Assert
        location.Should().NotBeNull();
        Js.Invoked.Should().Contain("getCurrent");
    }

    [Fact]
    public async Task ItShouldReturnNothingAndLogWhenTheBrowserReturnsNoResult()
    {
        // Arrange
        Js.Permission = "granted";
        Js.CurrentJson = "null";

        // Act
        var location = await Sut.TryCaptureAsync(false, CancellationToken.None);

        // Assert
        location.Should().BeNull();
        await MockDiagnostics.Received(1).LogAsync(
            DiagnosticLevel.Warning,
            DiagnosticEventNames.LocationCaptureFailed,
            "Location capture failed.",
            Arg.Any<IReadOnlyDictionary<string, string>?>(),
            Arg.Any<Exception?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRememberADeniedPermissionAndLogItAsInformation()
    {
        // Arrange
        Js.Permission = "granted";
        Js.CurrentJson = """{"error":"denied"}""";

        // Act
        var location = await Sut.TryCaptureAsync(false, CancellationToken.None);

        // Assert
        location.Should().BeNull();
        Js.Invoked.Should().Contain("setPromptDismissed");
        await MockDiagnostics.Received(1).LogAsync(
            DiagnosticLevel.Information,
            DiagnosticEventNames.LocationPermissionDenied,
            "Location permission denied.",
            Arg.Any<IReadOnlyDictionary<string, string>?>(),
            Arg.Any<Exception?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldLogAnUnavailablePositionWithoutDismissingThePrompt()
    {
        // Arrange
        Js.Permission = "granted";
        Js.CurrentJson = """{"error":"unavailable"}""";

        // Act
        var location = await Sut.TryCaptureAsync(false, CancellationToken.None);

        // Assert
        location.Should().BeNull();
        Js.Invoked.Should().NotContain("setPromptDismissed");
        await MockDiagnostics.Received(1).LogAsync(
            DiagnosticLevel.Warning,
            DiagnosticEventNames.LocationCaptureFailed,
            "Location capture failed.",
            Arg.Any<IReadOnlyDictionary<string, string>?>(),
            Arg.Any<Exception?>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(91, 0)]
    [InlineData(-91, 0)]
    [InlineData(0, 181)]
    [InlineData(0, -181)]
    public async Task ItShouldRejectACoordinateOutsideTheValidRange(double latitude, double longitude)
    {
        // Arrange
        Js.Permission = "granted";
        Js.CurrentJson = $$"""{"latitude":{{latitude}},"longitude":{{longitude}}}""";

        // Act
        var location = await Sut.TryCaptureAsync(false, CancellationToken.None);

        // Assert
        location.Should().BeNull();
        await MockDiagnostics.Received(1).LogAsync(
            DiagnosticLevel.Warning,
            DiagnosticEventNames.LocationCaptureFailed,
            "Location capture failed.",
            Arg.Any<IReadOnlyDictionary<string, string>?>(),
            Arg.Any<Exception?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldLogAndReturnNothingWhenTheBrowserThrows()
    {
        // Arrange
        Js.Permission = "granted";
        var failure = new JSException("geolocation failed");
        Js.CurrentFailure = failure;

        // Act
        var location = await Sut.TryCaptureAsync(false, CancellationToken.None);

        // Assert
        location.Should().BeNull();
        await MockDiagnostics.Received(1).LogAsync(
            DiagnosticLevel.Warning,
            DiagnosticEventNames.LocationCaptureFailed,
            "Location capture failed.",
            Arg.Any<IReadOnlyDictionary<string, string>?>(),
            failure,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldTreatATimedOutMessageAsABoundedFailure()
    {
        // Arrange
        Js.Permission = "granted";
        Js.CurrentFailure = new InvalidOperationException("The operation timed out.");

        // Act
        var location = await Sut.TryCaptureAsync(false, CancellationToken.None);

        // Assert
        location.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldPropagateACallerCancellation()
    {
        // Arrange
        Js.Permission = "granted";
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var act = () => Sut.TryCaptureAsync(false, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ItShouldCapturePreciseCoordinatesAsPrivateDeviceGps()
    {
        // Arrange
        Js.Permission = "granted";
        Js.CurrentJson = ValidFix;

        // Act
        var location = await Sut.TryCaptureAsync(false, CancellationToken.None);

        // Assert
        location!.Latitude.Should().Be(53.3498);
        location.Longitude.Should().Be(-6.2603);
        location.AccuracyMetres.Should().Be(12.5);
        location.CapturedOn.Should().Be(DateTimeOffset.Parse("2026-08-17T08:00:00Z"));
        location.Source.Should().Be(LocationDefaults.DeviceGps);
        location.Visibility.Should().Be(LocationDefaults.Private);
        location.ConsentVersion.Should().Be(LocationDefaults.ConsentVersion);
    }

    [Fact]
    public async Task ItShouldUseTheCurrentTimeWhenTheBrowserTimestampCannotBeParsed()
    {
        // Arrange
        Js.Permission = "granted";
        Js.CurrentJson = """{"latitude":53.3498,"longitude":-6.2603,"timestamp":"not a time"}""";
        var before = DateTimeOffset.UtcNow;

        // Act
        var location = await Sut.TryCaptureAsync(false, CancellationToken.None);

        // Assert
        location!.CapturedOn.Should().BeOnOrAfter(before).And.BeOnOrBefore(DateTimeOffset.UtcNow);
        location.AccuracyMetres.Should().BeNull();
    }
}
