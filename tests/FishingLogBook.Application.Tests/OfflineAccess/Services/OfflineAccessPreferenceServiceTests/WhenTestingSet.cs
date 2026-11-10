using AwesomeAssertions;
using FishingLogBook.Application.OfflineAccess.Contracts.Repositories;
using FishingLogBook.Application.OfflineAccess.Services;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.OfflineAccess.Services.OfflineAccessPreferenceServiceTests;

public class WhenTestingSet
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly IOfflineAccessPreferenceRepository _mockRepository =
        Substitute.For<IOfflineAccessPreferenceRepository>();
    private readonly OfflineAccessPreferenceService _sut;

    public WhenTestingSet()
    {
        _sut = new OfflineAccessPreferenceService(_mockRepository);
    }

    [Fact]
    public async Task ItShouldReturnFailureWhenTheRepositoryFails()
    {
        // Arrange
        _mockRepository.SetAsync(UserId, true, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<OfflineAccessPreferenceDto>("The repository failed."));

        // Act
        var result = await _sut.SetAsync(UserId, true, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("The repository failed.");
        await _mockRepository.Received(1).SetAsync(UserId, true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPersistTheRequestedStateForTheUser()
    {
        // Arrange
        var preference = new OfflineAccessPreferenceDto(false);
        _mockRepository.SetAsync(UserId, false, Arg.Any<CancellationToken>()).Returns(Result.Ok(preference));

        // Act
        var result = await _sut.SetAsync(UserId, false, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(preference);
        await _mockRepository.Received(1).SetAsync(UserId, false, Arg.Any<CancellationToken>());
        await _mockRepository.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
