using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Authentication.Enums;

namespace FishingLogBook.Mobile.Tests.Features.Authentication.ViewModels.AuthenticationViewModelTests;

public class WhenTestingSignInCompletion : BaseAuthenticationViewModelTest
{
    [Fact]
    public async Task ItShouldStopWaitingAndExplainWhenTheBrowserCallbackFails()
    {
        // Arrange
        Sut.Attach(Dispatch);
        await Sut.SignInAsync();
        // Act
        RaiseSignInCompleted(AuthenticationResultEnum.Invalid);
        // Assert
        Sut.IsPending.Should().BeFalse();
        Sut.CanSignIn.Should().BeTrue();
        Sut.CanCancel.Should().BeFalse();
        Sut.StatusText.Should().Be(Text["SessionExpired"]);
        DispatchCount.Should().Be(1);
    }

    [Fact]
    public async Task ItShouldIgnoreCallbackResultsAfterThePageIsDetached()
    {
        // Arrange
        Sut.Attach(Dispatch);
        await Sut.SignInAsync();
        Sut.Detach();
        // Act
        RaiseSignInCompleted(AuthenticationResultEnum.Invalid);
        // Assert
        Sut.IsPending.Should().BeTrue();
        Sut.StatusText.Should().Be(Text["SigningIn"]);
        DispatchCount.Should().Be(0);
    }
}
