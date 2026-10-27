using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Authentication.Enums;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Mobile.Tests.Features.Authentication.Handlers.AuthenticationCallbackHandlerTests;

public class WhenTestingForward : BaseAuthenticationCallbackHandlerTest
{
    [Theory]
    [InlineData(null)]
    [InlineData("not a uri")]
    [InlineData("https://evil.test/oauth2redirect?code=code")]
    public async Task ItShouldIgnoreUrisThatAreNotRegisteredAuthenticationCallbacks(string? callbackUri)
    {
        // Arrange
        // Act
        await Sut.ForwardAsync(callbackUri);
        // Assert
        await MockAuthenticationService.DidNotReceive().CompleteSignInAsync(
            Arg.Any<Uri>(),
            Arg.Any<CancellationToken>());
        Logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldContainAnAsynchronousSignInFailureAndLogOnlyTheExceptionType()
    {
        // Arrange
        MockAuthenticationService.CompleteSignInAsync(Arg.Any<Uri>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<AuthenticationResultEnum>(
                new InvalidOperationException($"Failed for {LoginCallback}")));
        // Act
        var action = () => Sut.ForwardAsync(LoginCallback);
        await action.Should().NotThrowAsync();
        // Assert
        Logger.Entries.Should().ContainSingle();
        Logger.Entries[0].Message.Should().Contain(typeof(InvalidOperationException).FullName);
        Logger.Entries[0].Message.Should().NotContain("secret-code").And.NotContain("secret-state");
        Logger.Entries[0].Exception.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldContainASynchronousSignInFailureAndLogOnlyTheExceptionType()
    {
        // Arrange
        MockAuthenticationService.CompleteSignInAsync(Arg.Any<Uri>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException($"Failed for {LoginCallback}"));
        // Act
        var action = () => Sut.ForwardAsync(LoginCallback);
        await action.Should().NotThrowAsync();
        // Assert
        Logger.Entries.Should().ContainSingle();
        Logger.Entries[0].Message.Should().NotContain("secret-code").And.NotContain("secret-state");
        Logger.Entries[0].Exception.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldAcknowledgeTheSignedOutCallbackWithoutCompletingSignIn()
    {
        // Arrange
        // Act
        await Sut.ForwardAsync(LogoutCallback);
        // Assert
        await MockAuthenticationService.DidNotReceive().CompleteSignInAsync(
            Arg.Any<Uri>(),
            Arg.Any<CancellationToken>());
        Logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldForwardTheLoginCallbackToSignInCompletion()
    {
        // Arrange
        MockAuthenticationService.CompleteSignInAsync(Arg.Any<Uri>(), Arg.Any<CancellationToken>())
            .Returns(AuthenticationResultEnum.Success);
        // Act
        await Sut.ForwardAsync(LoginCallback);
        // Assert
        await MockAuthenticationService.Received(1).CompleteSignInAsync(
            Arg.Is<Uri>(callback => callback.AbsoluteUri == new Uri(LoginCallback).AbsoluteUri),
            CancellationToken.None);
        Logger.Entries.Should().BeEmpty();
    }
}
