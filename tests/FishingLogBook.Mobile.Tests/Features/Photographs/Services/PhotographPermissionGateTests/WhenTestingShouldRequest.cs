using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Photographs.Enums;
using FishingLogBook.Mobile.Features.Photographs.Services;

namespace FishingLogBook.Mobile.Tests.Features.Photographs.Services.PhotographPermissionGateTests;

public sealed class WhenTestingShouldRequest
{
    [Fact]
    public void ItShouldRequestTheFirstTimeOnIosWhenStatusIsNotDetermined()
    {
        // Arrange
        // Act
        var request = PhotographPermissionGate.ShouldRequest(
            PhotographPermissionStatusEnum.NotDetermined,
            isIos: true,
            canPromptAgain: false);

        // Assert
        request.Should().BeTrue();
    }

    [Fact]
    public void ItShouldNotRequestAgainOnIosAfterTheUserHasDeniedAccess()
    {
        // Arrange
        // Act
        var request = PhotographPermissionGate.ShouldRequest(
            PhotographPermissionStatusEnum.Denied,
            isIos: true,
            canPromptAgain: false);

        // Assert
        request.Should().BeFalse();
    }

    [Fact]
    public void ItShouldStillRequestOnAndroidWhenAccessIsDeniedBecauseTheOsMayShowRationale()
    {
        // Arrange
        // Act
        var request = PhotographPermissionGate.ShouldRequest(
            PhotographPermissionStatusEnum.Denied,
            isIos: false,
            canPromptAgain: false);

        // Assert
        request.Should().BeTrue();
    }

    [Fact]
    public void ItShouldRequestOnIosWhenTheOsSaysThePromptCanBeShownAgain()
    {
        // Arrange
        // Act
        var request = PhotographPermissionGate.ShouldRequest(
            PhotographPermissionStatusEnum.Denied,
            isIos: true,
            canPromptAgain: true);

        // Assert
        request.Should().BeTrue();
    }

    [Theory]
    [InlineData(PhotographPermissionStatusEnum.Granted)]
    [InlineData(PhotographPermissionStatusEnum.PermanentlyDenied)]
    [InlineData(PhotographPermissionStatusEnum.Unavailable)]
    public void ItShouldNotRequestWhenAccessIsAlreadySettled(PhotographPermissionStatusEnum status)
    {
        // Arrange
        // Act
        var request = PhotographPermissionGate.ShouldRequest(status, isIos: true, canPromptAgain: true);

        // Assert
        request.Should().BeFalse();
    }
}
