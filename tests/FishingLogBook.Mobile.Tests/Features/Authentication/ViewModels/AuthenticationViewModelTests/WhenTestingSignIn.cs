using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Authentication.Enums;
using FishingLogBook.Mobile.Features.Authentication.ViewModels;
using FishingLogBook.Shared.Enums;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Authentication.ViewModels.AuthenticationViewModelTests;

public class WhenTestingSignIn : BaseAuthenticationViewModelTest
{
    [Fact]
    public async Task ItShouldNotStartSignInForAProviderThatIsNotAvailable()
    {
        // Arrange
        MockProviders.AvailableProviders.Returns([SignInProviderEnum.Google]);

        // Act
        await Sut.SignInAsync(SignInProviderEnum.Apple);

        // Assert
        await MockAuthenticationService.DidNotReceive().BeginSignInAsync(Arg.Any<CancellationToken>(), Arg.Any<SignInProviderEnum>());
        Sut.IsBusy.Should().BeFalse();
        Sut.IsPending.Should().BeFalse();
    }

    [Fact]
    public void ItShouldHideProviderButtonsWhenNoneAreAvailable()
    {
        // Arrange
        MockProviders.AvailableProviders.Returns(Array.Empty<SignInProviderEnum>());

        // Act
        var sut = new AuthenticationViewModel(MockAuthenticationService, MockProviders, Text);

        // Assert
        sut.ShowApple.Should().BeFalse();
        sut.ShowGoogle.Should().BeFalse();
        sut.HasSocialProviders.Should().BeFalse();
    }

    [Fact]
    public void ItShouldShowOnlyTheAvailableProviderButtons()
    {
        // Arrange
        MockProviders.AvailableProviders.Returns([SignInProviderEnum.Google]);

        // Act
        var sut = new AuthenticationViewModel(MockAuthenticationService, MockProviders, Text);

        // Assert
        sut.ShowGoogle.Should().BeTrue();
        sut.ShowApple.Should().BeFalse();
        sut.HasSocialProviders.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldReportAFailureAndAllowRetryWhenTheProviderCannotBeStarted()
    {
        // Arrange
        MockAuthenticationService.BeginSignInAsync(CancellationToken.None, SignInProviderEnum.Google)
            .Returns(AuthenticationResultEnum.TemporarilyUnavailable);

        // Act
        await Sut.SignInAsync(SignInProviderEnum.Google);

        // Assert
        Sut.StatusText.Should().Be(Text["SignInFailed"]);
        Sut.IsPending.Should().BeFalse();
        Sut.CanSignIn.Should().BeTrue();
    }

    [Theory]
    [InlineData(SignInProviderEnum.Google)]
    [InlineData(SignInProviderEnum.Apple)]
    public async Task ItShouldBeginSignInWithTheSelectedProviderAndWaitForTheBrowser(SignInProviderEnum provider)
    {
        // Arrange
        MockAuthenticationService.BeginSignInAsync(CancellationToken.None, provider)
            .Returns(AuthenticationResultEnum.Pending);

        // Act
        await Sut.SignInAsync(provider);

        // Assert
        await MockAuthenticationService.Received(1).BeginSignInAsync(CancellationToken.None, provider);
        Sut.IsPending.Should().BeTrue();
        Sut.StatusText.Should().Be(Text["SigningIn"]);
    }

    [Fact]
    public async Task ItShouldKeepEmailPasswordSignInAvailableWithoutSelectingAProvider()
    {
        // Arrange
        // Act
        await Sut.SignInAsync();

        // Assert
        await MockAuthenticationService.Received(1).BeginSignInAsync(CancellationToken.None, SignInProviderEnum.EmailPassword);
        Sut.IsPending.Should().BeTrue();
    }
}
