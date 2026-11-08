using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Enums;

namespace FishingLogBook.Shared.Tests.Constants.SignInProviderConstantsTests;

public class WhenTestingToCognitoIdentityProvider
{
    [Fact]
    public void ItShouldNotSelectAProviderForEmailPasswordSignIn()
    {
        // Arrange
        // Act
        var result = SignInProviderConstants.ToCognitoIdentityProvider(SignInProviderEnum.EmailPassword);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ItShouldMapGoogleToTheCognitoGoogleProviderName()
    {
        // Arrange
        // Act
        var result = SignInProviderConstants.ToCognitoIdentityProvider(SignInProviderEnum.Google);

        // Assert
        result.Should().Be("Google");
    }

    [Fact]
    public void ItShouldMapAppleToTheCognitoSignInWithAppleProviderName()
    {
        // Arrange
        // Act
        var result = SignInProviderConstants.ToCognitoIdentityProvider(SignInProviderEnum.Apple);

        // Assert
        result.Should().Be("SignInWithApple");
    }
}
