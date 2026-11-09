using AwesomeAssertions;
using FishingLogBook.Application.Common.Behaviours;
using FishingLogBook.Application.Common.Responses;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Common.Behaviours.ValidationBehaviourTests;

public class WhenTestingHandle
{
    private readonly Func<Task<TestResponse>> _mockNext = Substitute.For<Func<Task<TestResponse>>>();

    [Fact]
    public async Task ItShouldReturnTheFailuresFromEveryValidatorWithoutCallingTheHandler()
    {
        // Arrange
        var first = Substitute.For<IValidator<TestRequest>>();
        first.ValidateAsync(Arg.Any<IValidationContext>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult([new ValidationFailure("Name", "Name is required.")]));
        var second = Substitute.For<IValidator<TestRequest>>();
        second.ValidateAsync(Arg.Any<IValidationContext>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult([new ValidationFailure("Age", "Age is invalid.")]));
        var sut = new ValidationBehaviour<TestRequest, TestResponse>([first, second]);

        // Act
        var response = await sut.Handle(new TestRequest(), _ => _mockNext(), CancellationToken.None);

        // Assert
        response.IsFailure.Should().BeTrue();
        response.ValidationErrors!.Select(error => error.ErrorMessage)
            .Should().Equal("Name is required.", "Age is invalid.");
        await _mockNext.DidNotReceive().Invoke();
    }

    [Fact]
    public async Task ItShouldCallTheHandlerWhenNoValidatorIsRegistered()
    {
        // Arrange
        var expected = new TestResponse();
        _mockNext.Invoke().Returns(expected);
        var sut = new ValidationBehaviour<TestRequest, TestResponse>([]);

        // Act
        var response = await sut.Handle(new TestRequest(), _ => _mockNext(), CancellationToken.None);

        // Assert
        response.Should().BeSameAs(expected);
        await _mockNext.Received(1).Invoke();
    }

    [Fact]
    public async Task ItShouldCallTheHandlerWhenEveryValidatorPasses()
    {
        // Arrange
        var expected = new TestResponse();
        _mockNext.Invoke().Returns(expected);
        var validator = Substitute.For<IValidator<TestRequest>>();
        validator.ValidateAsync(Arg.Any<IValidationContext>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());
        var sut = new ValidationBehaviour<TestRequest, TestResponse>([validator]);

        // Act
        var response = await sut.Handle(new TestRequest(), _ => _mockNext(), CancellationToken.None);

        // Assert
        response.Should().BeSameAs(expected);
        response.ValidationErrors.Should().BeNull();
        await _mockNext.Received(1).Invoke();
        await validator.Received(1).ValidateAsync(
            Arg.Any<IValidationContext>(),
            Arg.Any<CancellationToken>());
    }

    public sealed class TestRequest : IRequest<TestResponse>
    {
    }

    public sealed class TestResponse : ValidatedResponse
    {
    }
}
