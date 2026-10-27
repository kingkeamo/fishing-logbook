using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Onboarding.Enums;
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
        await MockLocation.DidNotReceive().RequestWhenInUseAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRequestPermissionOnlyWhenTheUserAllowsLocation()
    {
        // Arrange
        await ReachLocationAsync();
        MockLocation.RequestWhenInUseAsync(Arg.Any<CancellationToken>())
            .Returns(LocationPermissionOutcomeEnum.Granted);

        // Act
        await Sut.AllowLocationAsync(CancellationToken.None);

        // Assert
        Sut.LocationHandled.Should().BeTrue();
        Sut.CanContinue.Should().BeTrue();
        Sut.Message.Should().Be(Text["OnboardingLocationHandled"]);
        await MockLocation.Received(1).RequestWhenInUseAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(LocationPermissionOutcomeEnum.Denied)]
    [InlineData(LocationPermissionOutcomeEnum.PermanentlyDenied)]
    [InlineData(LocationPermissionOutcomeEnum.Unavailable)]
    public async Task ItShouldAllowOnboardingToContinueWhenPermissionIsNotGranted(
        LocationPermissionOutcomeEnum outcome)
    {
        // Arrange
        await ReachLocationAsync();
        MockLocation.RequestWhenInUseAsync(Arg.Any<CancellationToken>()).Returns(outcome);

        // Act
        await Sut.AllowLocationAsync(CancellationToken.None);
        await Sut.ContinueAsync(CancellationToken.None);

        // Assert
        Sut.StepNumber.Should().Be(4);
        Sut.LocationHandled.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldSkipPermissionWhenTheUserChoosesNotNow()
    {
        // Arrange
        await ReachLocationAsync();

        // Act
        Sut.SkipLocation();
        await Sut.ContinueAsync(CancellationToken.None);

        // Assert
        Sut.StepNumber.Should().Be(4);
        await MockLocation.DidNotReceive().RequestWhenInUseAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotRequestPermissionAgainDuringTheSameFlow()
    {
        // Arrange
        await ReachLocationAsync();
        MockLocation.RequestWhenInUseAsync(Arg.Any<CancellationToken>())
            .Returns(LocationPermissionOutcomeEnum.Denied);
        await Sut.AllowLocationAsync(CancellationToken.None);

        // Act
        await Sut.AllowLocationAsync(CancellationToken.None);
        Sut.SkipLocation();
        Sut.Back();
        await Sut.ContinueAsync(CancellationToken.None);

        // Assert
        await MockLocation.Received(1).RequestWhenInUseAsync(Arg.Any<CancellationToken>());
        Sut.CanChooseLocation.Should().BeFalse();
        Sut.CanContinue.Should().BeTrue();
    }
}
