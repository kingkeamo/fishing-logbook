using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Photographs.Enums;
using FishingLogBook.Mobile.Features.Photographs.Services;

namespace FishingLogBook.Mobile.Tests.Features.Photographs.Services.PhotographPermissionGateTests;

public sealed class WhenTestingCanCapture
{
    [Theory]
    [InlineData(PhotographPermissionStatusEnum.Granted, true)]
    [InlineData(PhotographPermissionStatusEnum.NotDetermined, false)]
    [InlineData(PhotographPermissionStatusEnum.Denied, false)]
    [InlineData(PhotographPermissionStatusEnum.PermanentlyDenied, false)]
    [InlineData(PhotographPermissionStatusEnum.Unavailable, false)]
    public void ItShouldAllowCaptureOnlyWhenPermissionIsGranted(
        PhotographPermissionStatusEnum status,
        bool expected)
    {
        // Arrange
        // Act
        var allowed = PhotographPermissionGate.CanCapture(status);

        // Assert
        allowed.Should().Be(expected);
    }
}
