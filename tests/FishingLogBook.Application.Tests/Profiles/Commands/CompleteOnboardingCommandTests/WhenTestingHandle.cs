using AwesomeAssertions;
using FishingLogBook.Application.Profiles.Commands;
using FishingLogBook.Application.Profiles.Contracts.Services;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Profiles.Commands.CompleteOnboardingCommandTests;

public class WhenTestingHandle
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly IProfileService _mockService = Substitute.For<IProfileService>();
    private readonly CompleteOnboardingHandler _sut;

    public WhenTestingHandle()
    {
        _sut = new CompleteOnboardingHandler(_mockService);
    }

    [Fact]
    public async Task ItShouldReturnFailureWhenTheServiceFails()
    {
        // Arrange
        _mockService.CompleteOnboardingAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<ProfileDto>("The service failed."));

        // Act
        var response = await _sut.Handle(new CompleteOnboardingCommand { UserId = UserId }, CancellationToken.None);

        // Assert
        response.IsFailure.Should().BeTrue();
        response.ErrorMessage.Should().Be("The service failed.");
        response.Profile.Should().BeNull();
        await _mockService.Received(1).CompleteOnboardingAsync(
            UserId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheServiceResult()
    {
        // Arrange
        var payload = new ProfileDto(UserId, "Eamonn", null, null, null, "Westmeath", true, false, true, true, false, OnboardingCompleted: true);
        _mockService.CompleteOnboardingAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(payload));

        // Act
        var response = await _sut.Handle(new CompleteOnboardingCommand { UserId = UserId }, CancellationToken.None);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Profile.Should().Be(payload);
        await _mockService.Received(1).CompleteOnboardingAsync(
            UserId,
            Arg.Any<CancellationToken>());
    }
}
