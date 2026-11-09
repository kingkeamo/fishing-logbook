using AwesomeAssertions;
using FishingLogBook.Application.OfflineAccess.Commands;
using FishingLogBook.Application.OfflineAccess.Contracts.Services;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.OfflineAccess.Commands.UpdateOfflineAccessPreferenceCommandTests;

public class WhenTestingHandle
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly IOfflineAccessPreferenceService _mockService = Substitute.For<IOfflineAccessPreferenceService>();
    private readonly UpdateOfflineAccessPreferenceHandler _sut;

    public WhenTestingHandle()
    {
        _sut = new UpdateOfflineAccessPreferenceHandler(_mockService);
    }

    [Fact]
    public async Task ItShouldReturnFailureWhenTheServiceFails()
    {
        // Arrange
        _mockService.SetAsync(UserId, true, Arg.Any<CancellationToken>())
            .Returns(Result.Fail<OfflineAccessPreferenceDto>("The service failed."));

        // Act
        var response = await _sut.Handle(
            new UpdateOfflineAccessPreferenceCommand { UserId = UserId, Enabled = true },
            CancellationToken.None);

        // Assert
        response.IsFailure.Should().BeTrue();
        response.ErrorMessage.Should().Be("The service failed.");
        response.Preference.Should().BeNull();
        await _mockService.Received(1).SetAsync(UserId, true, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ItShouldPassTheRequestedStateToTheServiceAndReturnThePreference(bool enabled)
    {
        // Arrange
        var preference = new OfflineAccessPreferenceDto(enabled);
        _mockService.SetAsync(UserId, enabled, Arg.Any<CancellationToken>())
            .Returns(Result.Ok(preference));

        // Act
        var response = await _sut.Handle(
            new UpdateOfflineAccessPreferenceCommand { UserId = UserId, Enabled = enabled },
            CancellationToken.None);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Preference.Should().Be(preference);
        await _mockService.Received(1).SetAsync(UserId, enabled, Arg.Any<CancellationToken>());
        await _mockService.DidNotReceive().SetAsync(UserId, !enabled, Arg.Any<CancellationToken>());
    }
}
