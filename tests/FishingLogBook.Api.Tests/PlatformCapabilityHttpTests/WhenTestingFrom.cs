using AwesomeAssertions;
using FishingLogBook.Api.Authorization;
using FishingLogBook.Application.Capabilities.Errors;
using FishingLogBook.Application.Common.Contracts.Services;
using FishingLogBook.Application.Common.Responses;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;

namespace FishingLogBook.Api.Tests.PlatformCapabilityHttpTests;

public class WhenTestingFrom
{
    private readonly ICurrentUser _mockCurrentUser = Substitute.For<ICurrentUser>();

    public WhenTestingFrom()
    {
        _mockCurrentUser.IsResolved.Returns(true);
    }

    [Fact]
    public void ItShouldReturnUnauthorizedWhenTheCurrentUserIsNotResolved()
    {
        // Arrange
        _mockCurrentUser.IsResolved.Returns(false);

        // Act
        var result = PlatformCapabilityHttp.From(_mockCurrentUser, new TestResponse());

        // Assert
        result.Should().BeOfType<UnauthorizedHttpResult>();
        _ = _mockCurrentUser.Received(1).IsResolved;
    }

    [Fact]
    public void ItShouldReturnUnauthorizedWhenTheResponseSaysTheUserIsUnresolved()
    {
        // Arrange
        var response = ValidatedResponse.FromError<TestResponse>(new CurrentUserUnresolvedError());

        // Act
        var result = PlatformCapabilityHttp.From(_mockCurrentUser, response);

        // Assert
        result.Should().BeOfType<UnauthorizedHttpResult>();
    }

    [Fact]
    public void ItShouldReturnForbiddenWithTheResponseWhenTheCapabilityIsMissing()
    {
        // Arrange
        var response = ValidatedResponse.FromError<TestResponse>(new MissingPlatformCapabilityError());

        // Act
        var result = PlatformCapabilityHttp.From(_mockCurrentUser, response);

        // Assert
        result.Should().BeAssignableTo<IStatusCodeHttpResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public void ItShouldReturnBadRequestForValidationErrors()
    {
        // Arrange
        var response = new TestResponse { ValidationErrors = [new ValidationFailure("Name", "Required")] };

        // Act
        var result = PlatformCapabilityHttp.From(_mockCurrentUser, response);

        // Assert
        result.Should().BeOfType<BadRequest<ValidatedResponse>>()
            .Which.Value.Should().BeSameAs(response);
    }

    [Fact]
    public void ItShouldReturnServiceUnavailableForAnyOtherFailure()
    {
        // Arrange
        var response = new TestResponse { ErrorMessage = "The database is down." };

        // Act
        var result = PlatformCapabilityHttp.From(_mockCurrentUser, response);

        // Assert
        var problem = result.Should().BeOfType<ProblemHttpResult>().Subject;
        problem.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        problem.ProblemDetails.Title.Should().Be("The database is down.");
    }

    [Fact]
    public void ItShouldReturnNoContentWhenTheOperationSucceeds()
    {
        // Arrange
        // Act
        var result = PlatformCapabilityHttp.From(_mockCurrentUser, new TestResponse());

        // Assert
        result.Should().BeOfType<NoContent>();
    }

    private sealed class TestResponse : ValidatedResponse
    {
    }
}
