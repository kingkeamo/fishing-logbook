using AwesomeAssertions;
using FishingLogBook.Application.Users.Commands;
using FishingLogBook.Shared.Constants;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace FishingLogBook.Api.Tests.CurrentUserMiddlewareTests;

public class WhenTestingInvoke : BaseCurrentUserMiddlewareTest
{
    [Theory]
    [InlineData(null, "angler@example.test")]
    [InlineData("", "angler@example.test")]
    [InlineData("cognito-subject", null)]
    [InlineData("cognito-subject", " ")]
    public async Task ItShouldRejectAnAuthenticatedRequestMissingTheSubjectOrEmail(string? subject, string? email)
    {
        // Arrange
        var context = Authenticated(subject, email);

        // Act
        await Sut.InvokeAsync(context, MockMediator, MockCurrentUser);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        NextCalled.Should().BeFalse();
        await MockMediator.DidNotReceive().Send(Arg.Any<ResolveCurrentUserCommand>(), Arg.Any<CancellationToken>());
        MockCurrentUser.DidNotReceive().Assign(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ItShouldRejectWithServiceUnavailableWhenResolutionThrows()
    {
        // Arrange
        var context = Authenticated();
        MockMediator.Send(Arg.Any<ResolveCurrentUserCommand>(), Arg.Any<CancellationToken>())
            .Returns<ResolveCurrentUserResponse>(_ => throw new InvalidOperationException("database down"));

        // Act
        await Sut.InvokeAsync(context, MockMediator, MockCurrentUser);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        NextCalled.Should().BeFalse();
        await MockMediator.Received(1).Send(Arg.Any<ResolveCurrentUserCommand>(), Arg.Any<CancellationToken>());
        MockCurrentUser.DidNotReceive().Assign(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ItShouldRethrowWhenTheRequestWasAbortedDuringResolution()
    {
        // Arrange
        using var aborted = new CancellationTokenSource();
        var context = Authenticated(requestAborted: aborted.Token);
        MockMediator.Send(Arg.Any<ResolveCurrentUserCommand>(), Arg.Any<CancellationToken>())
            .Returns<ResolveCurrentUserResponse>(_ =>
            {
                aborted.Cancel();
                throw new OperationCanceledException(aborted.Token);
            });

        // Act
        var act = () => Sut.InvokeAsync(context, MockMediator, MockCurrentUser);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        NextCalled.Should().BeFalse();
        await MockMediator.Received(1).Send(Arg.Any<ResolveCurrentUserCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectWithUnauthorizedWhenTheResponseHasValidationErrors()
    {
        // Arrange
        var context = Authenticated();
        MockMediator.Send(Arg.Any<ResolveCurrentUserCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ResolveCurrentUserResponse
            {
                ValidationErrors = [new ValidationFailure("Subject", "required")]
            });

        // Act
        await Sut.InvokeAsync(context, MockMediator, MockCurrentUser);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        NextCalled.Should().BeFalse();
        await MockMediator.Received(1).Send(Arg.Any<ResolveCurrentUserCommand>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("External identity is missing.", StatusCodes.Status401Unauthorized)]
    [InlineData("Authenticated email is missing.", StatusCodes.Status401Unauthorized)]
    [InlineData("The user could not be created.", StatusCodes.Status503ServiceUnavailable)]
    public async Task ItShouldMapAFailedResolutionToTheMatchingStatus(string errorMessage, int expectedStatus)
    {
        // Arrange
        var context = Authenticated();
        MockMediator.Send(Arg.Any<ResolveCurrentUserCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ResolveCurrentUserResponse { ErrorMessage = errorMessage });

        // Act
        await Sut.InvokeAsync(context, MockMediator, MockCurrentUser);

        // Assert
        context.Response.StatusCode.Should().Be(expectedStatus);
        NextCalled.Should().BeFalse();
        await MockMediator.Received(1).Send(Arg.Any<ResolveCurrentUserCommand>(), Arg.Any<CancellationToken>());
        MockCurrentUser.DidNotReceive().Assign(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ItShouldContinueTheRequestWithoutResolvingAnUnauthenticatedCaller()
    {
        // Arrange
        var context = new DefaultHttpContext();

        // Act
        await Sut.InvokeAsync(context, MockMediator, MockCurrentUser);

        // Assert
        NextCalled.Should().BeTrue();
        await MockMediator.DidNotReceive().Send(Arg.Any<ResolveCurrentUserCommand>(), Arg.Any<CancellationToken>());
        MockCurrentUser.DidNotReceive().Assign(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ItShouldAssignTheResolvedUserFromTheValidatedClaimsAndContinue()
    {
        // Arrange
        var context = Authenticated("cognito-subject", "angler@example.test");
        MockMediator.Send(Arg.Any<ResolveCurrentUserCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ResolveCurrentUserResponse { UserId = UserId });

        // Act
        await Sut.InvokeAsync(context, MockMediator, MockCurrentUser);

        // Assert
        NextCalled.Should().BeTrue();
        await MockMediator.Received(1).Send(
            Arg.Is<ResolveCurrentUserCommand>(command => command.Provider == IdentityProviderConstants.Cognito
                && command.Subject == "cognito-subject"
                && command.Email == "angler@example.test"),
            Arg.Any<CancellationToken>());
        MockCurrentUser.Received(1).Assign(
            UserId,
            "angler@example.test",
            IdentityProviderConstants.Cognito,
            "cognito-subject");
    }
}
