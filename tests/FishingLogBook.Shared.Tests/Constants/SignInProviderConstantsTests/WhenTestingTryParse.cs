using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Enums;

namespace FishingLogBook.Shared.Tests.Constants.SignInProviderConstantsTests;

public class WhenTestingTryParse
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("facebook")]
    [InlineData("cognito")]
    public void ItShouldRejectMissingAndUnsupportedProviders(string? value)
    {
        // Arrange
        // Act
        var parsed = SignInProviderConstants.TryParse(value, out var provider);

        // Assert
        parsed.Should().BeFalse();
        provider.Should().Be(SignInProviderEnum.EmailPassword);
    }

    [Theory]
    [InlineData("google", SignInProviderEnum.Google)]
    [InlineData(" Google ", SignInProviderEnum.Google)]
    [InlineData("APPLE", SignInProviderEnum.Apple)]
    public void ItShouldParseSupportedProvidersIgnoringCaseAndWhitespace(string value, SignInProviderEnum expected)
    {
        // Arrange
        // Act
        var parsed = SignInProviderConstants.TryParse(value, out var provider);

        // Assert
        parsed.Should().BeTrue();
        provider.Should().Be(expected);
    }
}
