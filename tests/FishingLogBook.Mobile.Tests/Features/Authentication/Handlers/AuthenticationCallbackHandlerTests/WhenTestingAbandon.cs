using AwesomeAssertions;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Authentication.Handlers.AuthenticationCallbackHandlerTests;

public class WhenTestingAbandon : BaseAuthenticationCallbackHandlerTest
{
    [Fact]
    public async Task ItShouldCancelThePendingSignIn()
    {
        // Arrange
        // Act
        await Sut.AbandonAsync();
        // Assert
        await MockAuthenticationService.Received(1).CancelSignInAsync();
        Logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldContainACancellationFailureAndLogOnlyTheExceptionType()
    {
        // Arrange
        MockAuthenticationService.CancelSignInAsync()
            .Returns(Task.FromException(new InvalidOperationException($"Failed for {LoginCallback}")));
        // Act
        var action = Sut.AbandonAsync;
        await action.Should().NotThrowAsync();
        // Assert
        Logger.Entries.Should().ContainSingle();
        Logger.Entries[0].Message.Should().Contain(typeof(InvalidOperationException).FullName);
        Logger.Entries[0].Message.Should().NotContain("secret-code");
    }
}
