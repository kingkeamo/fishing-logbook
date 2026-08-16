using AwesomeAssertions;
using FishingLogBook.Application.Users.Commands;
using FishingLogBook.Shared.Constants;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Users.Commands.ResolveCurrentUserCommandTests;

public class WhenTestingHandleWhenTheUserIdIsEmpty : BaseResolveCurrentUserCommandTest
{
    [Fact]
    public async Task ItShouldFailWithoutUsingAFallbackUserId()
    {
        // Arrange
        const string subject = "cognito-subject";
        var command = new ResolveCurrentUserCommand
        {
            Provider = IdentityProviderConstants.Cognito,
            Subject = subject,
            Email = "eamonn@example.test"
        };
        MockUserIdentityService
            .ResolveAsync(Matching(command), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(Guid.Empty));

        // Act
        var response = await Sut.Handle(command, CancellationToken.None);

        // Assert
        response.IsFailure.Should().BeTrue();
        response.ErrorMessage.Should().Be("FishingLogBook UserId cannot be empty.");
        response.UserId.Should().Be(Guid.Empty);
        await MockUserIdentityService.Received(1).ResolveAsync(
            Matching(command),
            Arg.Any<CancellationToken>());
    }
}
