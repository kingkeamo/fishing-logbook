using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Onboarding.Enums;
using FishingLogBook.Mobile.Features.Onboarding.Models;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Onboarding.ViewModels.OnboardingViewModelTests;

public sealed class WhenTestingLocation : BaseOnboardingViewModelTest
{
    [Fact]
    public async Task ItShouldNotAdvanceUntilLocationIsHandled()
    {
        // Arrange
        await ReachLocationAsync();

        // Act
        await Sut.ContinueAsync(CancellationToken.None);

        // Assert
        Sut.StepNumber.Should().Be(3);
        Sut.CanContinue.Should().BeFalse();
        await MockLocation.DidNotReceive().CaptureCurrentAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldCaptureCoordinatesWhenPermissionIsGranted()
    {
        // Arrange
        await ReachLocationAsync();
        var captured = CapturedFix();
        MockLocation.CaptureCurrentAsync(Arg.Any<CancellationToken>()).Returns(captured);

        // Act
        await Sut.AllowLocationAsync(CancellationToken.None);

        // Assert
        Sut.LocationHandled.Should().BeTrue();
        Sut.CanContinue.Should().BeTrue();
        Sut.Message.Should().Be(Text["OnboardingLocationCaptured"]);
        await MockLocation.Received(1).CaptureCurrentAsync(Arg.Any<CancellationToken>());
        await MockEditor.Received(1).SaveAsync(
            Arg.Any<ProfileEditorDataModel>(),
            Arg.Any<UpdateProfileDto>(),
            Arg.Any<UpdateFishingPreferencesDto>(),
            Arg.Any<UpdateFishingLocationPreferencesDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldContinueWhenPermissionIsGrantedButLocationIsUnavailable()
    {
        // Arrange
        await ReachLocationAsync();
        MockLocation.CaptureCurrentAsync(Arg.Any<CancellationToken>())
            .Returns(DeviceLocationCaptureModel.WithoutCoordinates(LocationCaptureOutcomeEnum.Unavailable));

        // Act
        await Sut.AllowLocationAsync(CancellationToken.None);
        var message = Sut.Message;
        await Sut.ContinueAsync(CancellationToken.None);

        // Assert
        message.Should().Be(Text["OnboardingLocationUnavailable"]);
        Sut.StepNumber.Should().Be(4);
        Sut.LocationHandled.Should().BeTrue();
        await MockEditor.Received(1).SaveAsync(
            Arg.Any<ProfileEditorDataModel>(),
            Arg.Any<UpdateProfileDto>(),
            Arg.Any<UpdateFishingPreferencesDto>(),
            Arg.Any<UpdateFishingLocationPreferencesDto>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(LocationCaptureOutcomeEnum.Denied)]
    [InlineData(LocationCaptureOutcomeEnum.PermanentlyDenied)]
    public async Task ItShouldAllowOnboardingToContinueWhenPermissionIsDenied(
        LocationCaptureOutcomeEnum outcome)
    {
        // Arrange
        await ReachLocationAsync();
        MockLocation.CaptureCurrentAsync(Arg.Any<CancellationToken>())
            .Returns(DeviceLocationCaptureModel.WithoutCoordinates(outcome));

        // Act
        await Sut.AllowLocationAsync(CancellationToken.None);
        var message = Sut.Message;
        await Sut.ContinueAsync(CancellationToken.None);

        // Assert
        message.Should().Be(Text["OnboardingLocationDenied"]);
        Sut.StepNumber.Should().Be(4);
        Sut.LocationHandled.Should().BeTrue();
        await MockLocation.Received(1).CaptureCurrentAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldContinueWhenCaptureTimesOut()
    {
        // Arrange
        await ReachLocationAsync();
        MockLocation.CaptureCurrentAsync(Arg.Any<CancellationToken>())
            .Returns(DeviceLocationCaptureModel.WithoutCoordinates(LocationCaptureOutcomeEnum.Timeout));

        // Act
        await Sut.AllowLocationAsync(CancellationToken.None);
        var message = Sut.Message;
        await Sut.ContinueAsync(CancellationToken.None);

        // Assert
        message.Should().Be(Text["OnboardingLocationTimeout"]);
        Sut.StepNumber.Should().Be(4);
        await MockEditor.Received(1).SaveAsync(
            Arg.Any<ProfileEditorDataModel>(),
            Arg.Any<UpdateProfileDto>(),
            Arg.Any<UpdateFishingPreferencesDto>(),
            Arg.Any<UpdateFishingLocationPreferencesDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSkipCaptureWhenTheUserChoosesNotNow()
    {
        // Arrange
        await ReachLocationAsync();

        // Act
        Sut.SkipLocation();
        var message = Sut.Message;
        await Sut.ContinueAsync(CancellationToken.None);

        // Assert
        message.Should().Be(Text["OnboardingLocationSkipped"]);
        Sut.StepNumber.Should().Be(4);
        await MockLocation.DidNotReceive().CaptureCurrentAsync(Arg.Any<CancellationToken>());
        await MockEditor.Received(1).SaveAsync(
            Arg.Any<ProfileEditorDataModel>(),
            Arg.Any<UpdateProfileDto>(),
            Arg.Any<UpdateFishingPreferencesDto>(),
            Arg.Any<UpdateFishingLocationPreferencesDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotCaptureAgainDuringTheSameFlow()
    {
        // Arrange
        await ReachLocationAsync();
        MockLocation.CaptureCurrentAsync(Arg.Any<CancellationToken>()).Returns(CapturedFix());
        await Sut.AllowLocationAsync(CancellationToken.None);
        var message = Sut.Message;

        // Act
        await Sut.AllowLocationAsync(CancellationToken.None);
        Sut.SkipLocation();
        Sut.Back();
        await Sut.ContinueAsync(CancellationToken.None);

        // Assert
        message.Should().Be(Text["OnboardingLocationCaptured"]);
        await MockLocation.Received(1).CaptureCurrentAsync(Arg.Any<CancellationToken>());
        Sut.CanChooseLocation.Should().BeFalse();
        Sut.CanContinue.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldIgnoreAStaleCaptureAfterTheAccountChanges()
    {
        // Arrange
        await ReachLocationAsync();
        MockLocation.CaptureCurrentAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                MockSession.IsCurrent(Lease).Returns(false);
                MockAuthentication.CurrentSession.Returns((AuthenticationSessionModel?)null);
                return CapturedFix();
            });

        // Act
        await Sut.AllowLocationAsync(CancellationToken.None);

        // Assert
        Sut.LocationHandled.Should().BeFalse();
        Sut.CanContinue.Should().BeFalse();
        Sut.Message.Should().BeNull();
        Sut.StepNumber.Should().Be(3);
    }

    [Fact]
    public async Task ItShouldResetTheWizardWhenCaptureIsCancelledByAnAccountSwitch()
    {
        // Arrange
        await ReachLocationAsync();
        MockLocation.CaptureCurrentAsync(Arg.Any<CancellationToken>())
            .Returns<DeviceLocationCaptureModel>(_ => throw new OperationCanceledException());

        // Act
        await Sut.AllowLocationAsync(CancellationToken.None);

        // Assert
        Sut.StepNumber.Should().Be(1);
        Sut.LocationHandled.Should().BeFalse();
        Sut.Message.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldNotCaptureWhenTheSessionHasExpired()
    {
        // Arrange
        await ReachLocationAsync();
        MockSession.Capture().Returns((AccountSessionLeaseModel?)null);

        // Act
        await Sut.AllowLocationAsync(CancellationToken.None);

        // Assert
        Sut.LocationHandled.Should().BeFalse();
        Sut.CanContinue.Should().BeFalse();
        Sut.Message.Should().Be(Text["ProfileSessionExpired"]);
        await MockLocation.DidNotReceive().CaptureCurrentAsync(Arg.Any<CancellationToken>());
    }

    private static DeviceLocationCaptureModel CapturedFix()
    {
        return DeviceLocationCaptureModel.FromCoordinates(
            53.27,
            -9.05,
            12.4,
            DateTimeOffset.Parse("2026-10-27T12:00:00Z"));
    }
}
