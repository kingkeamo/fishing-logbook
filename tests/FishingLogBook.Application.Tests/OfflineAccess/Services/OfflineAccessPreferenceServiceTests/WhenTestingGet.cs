using AwesomeAssertions;
using FishingLogBook.Application.OfflineAccess.Contracts.Repositories;
using FishingLogBook.Application.OfflineAccess.Services;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.OfflineAccess.Services.OfflineAccessPreferenceServiceTests;

public class WhenTestingGet
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly IOfflineAccessPreferenceRepository _mockRepository =
        Substitute.For<IOfflineAccessPreferenceRepository>();
    private readonly OfflineAccessPreferenceService _sut;

    public WhenTestingGet()
    {
        _sut = new OfflineAccessPreferenceService(_mockRepository);
    }

    [Fact]
    public async Task ItShouldReturnFailureWhenTheRepositoryFails()
    {
        // Arrange
        _mockRepository.GetAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<OfflineAccessPreferenceDto>("The repository failed."));

        // Act
        var result = await _sut.GetAsync(UserId, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("The repository failed.");
        await _mockRepository.Received(1).GetAsync(UserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReturnTheStoredPreference()
    {
        // Arrange
        var preference = new OfflineAccessPreferenceDto(true, DateTimeOffset.Parse("2026-09-12T06:30:00Z"));
        _mockRepository.GetAsync(UserId, Arg.Any<CancellationToken>()).Returns(Result.Ok(preference));

        // Act
        var result = await _sut.GetAsync(UserId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(preference);
        await _mockRepository.Received(1).GetAsync(UserId, Arg.Any<CancellationToken>());
        await _mockRepository.DidNotReceive().SetAsync(
            Arg.Any<Guid>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }
}
