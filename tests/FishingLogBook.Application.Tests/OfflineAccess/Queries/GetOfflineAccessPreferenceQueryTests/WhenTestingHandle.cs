using AwesomeAssertions;
using FishingLogBook.Application.OfflineAccess.Contracts.Services;
using FishingLogBook.Application.OfflineAccess.Queries;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.OfflineAccess.Queries.GetOfflineAccessPreferenceQueryTests;

public class WhenTestingHandle
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly IOfflineAccessPreferenceService _mockService = Substitute.For<IOfflineAccessPreferenceService>();
    private readonly GetOfflineAccessPreferenceHandler _sut;

    public WhenTestingHandle()
    {
        _sut = new GetOfflineAccessPreferenceHandler(_mockService);
    }

    [Fact]
    public async Task ItShouldReturnFailureWhenTheServiceFails()
    {
        // Arrange
        _mockService.GetAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<OfflineAccessPreferenceDto>("The service failed."));

        // Act
        var response = await _sut.Handle(new GetOfflineAccessPreferenceQuery { UserId = UserId }, CancellationToken.None);

        // Assert
        response.IsFailure.Should().BeTrue();
        response.ErrorMessage.Should().Be("The service failed.");
        response.Preference.Should().BeNull();
        await _mockService.Received(1).GetAsync(
            UserId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheServiceResult()
    {
        // Arrange
        var payload = new OfflineAccessPreferenceDto(true, DateTimeOffset.Parse("2026-09-12T06:30:00Z"));
        _mockService.GetAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(payload));

        // Act
        var response = await _sut.Handle(new GetOfflineAccessPreferenceQuery { UserId = UserId }, CancellationToken.None);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Preference.Should().Be(payload);
        await _mockService.Received(1).GetAsync(
            UserId,
            Arg.Any<CancellationToken>());
    }
}
