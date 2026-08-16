using AwesomeAssertions;
using FishingLogBook.Application.Users.Commands;
using FishingLogBook.Shared.Constants;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Users.Commands.ResolveCurrentUserCommandTests;

public class WhenTestingHandle : BaseResolveCurrentUserCommandTest
{
    [Fact]
    public async Task ItShouldReturnTheResolvedUserId()
    {
        // Arrange
        const string subject = "cognito-subject";
        var userId = Guid.NewGuid();
        var command = new ResolveCurrentUserCommand
        {
            Provider = IdentityProviderConstants.Cognito,
            Subject = subject,
            Email = "eamonn@example.test"
        };
        MockUserIdentityService
            .ResolveAsync(Matching(command), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(userId));

        // Act
        var response = await Sut.Handle(command, CancellationToken.None);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.UserId.Should().Be(userId);
        response.UserId.Should().NotBe(Guid.Empty);
        await MockUserIdentityService.Received(1).ResolveAsync(
            Matching(command),
            Arg.Any<CancellationToken>());
    }
}
