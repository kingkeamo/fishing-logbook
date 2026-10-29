using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Authentication.Enums;
using FishingLogBook.Mobile.Features.Authentication.Models;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Authentication.Services.OidcRefreshServiceTests;

public sealed class WhenTestingRefresh : BaseOidcRefreshServiceTest
{
    [Fact]
    public async Task ItShouldNotRetryPermanentOAuthRejection()
    {
        // Arrange
        MockOidcClient.RefreshAsync(RefreshToken, Arg.Any<CancellationToken>())
            .Returns(OidcTokenResultModel.Failed(OidcFailureKindEnum.InvalidGrant));
        // Act
        var result = await Sut.RefreshAsync(RefreshToken, CancellationToken.None);
        // Assert
        result.Failure.Should().Be(OidcFailureKindEnum.InvalidGrant);
        await MockOidcClient.Received(1).RefreshAsync(RefreshToken, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotRetryAnInvalidResponse()
    {
        // Arrange
        MockOidcClient.RefreshAsync(RefreshToken, Arg.Any<CancellationToken>())
            .Returns(OidcTokenResultModel.Failed(OidcFailureKindEnum.InvalidResponse));
        // Act
        var result = await Sut.RefreshAsync(RefreshToken, CancellationToken.None);
        // Assert
        result.Failure.Should().Be(OidcFailureKindEnum.InvalidResponse);
        await MockOidcClient.Received(1).RefreshAsync(RefreshToken, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotRetryAfterTheRotationSafetyWindow()
    {
        // Arrange
        MockTimeProvider.GetTimestamp().Returns(0, TimeSpan.FromSeconds(4).Ticks);
        MockOidcClient.RefreshAsync(RefreshToken, Arg.Any<CancellationToken>())
            .Returns(OidcTokenResultModel.Failed(OidcFailureKindEnum.Transient));
        // Act
        var result = await Sut.RefreshAsync(RefreshToken, CancellationToken.None);
        // Assert
        result.Failure.Should().Be(OidcFailureKindEnum.Transient);
        await MockOidcClient.Received(1).RefreshAsync(RefreshToken, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRespectCallerCancellationWithoutRetrying()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        MockOidcClient.RefreshAsync(RefreshToken, Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                await cancellation.CancelAsync();
                call.Arg<CancellationToken>().ThrowIfCancellationRequested();
                return OidcTokenResultModel.Failed(OidcFailureKindEnum.Transient);
            });
        // Act
        var action = () => Sut.RefreshAsync(RefreshToken, cancellation.Token);
        // Assert
        await action.Should().ThrowAsync<OperationCanceledException>();
        await MockOidcClient.Received(1).RefreshAsync(RefreshToken, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRetryOneTransientFailureAndReturnTheSuccessfulRotation()
    {
        // Arrange
        var success = new OidcTokenResultModel(
            OidcFailureKindEnum.None,
            "access-token",
            "rotated-refresh-token",
            DateTimeOffset.UtcNow.AddHours(1),
            null,
            null);
        MockOidcClient.RefreshAsync(RefreshToken, Arg.Any<CancellationToken>())
            .Returns(
                OidcTokenResultModel.Failed(OidcFailureKindEnum.Transient),
                success);
        // Act
        var result = await Sut.RefreshAsync(RefreshToken, CancellationToken.None);
        // Assert
        result.Should().Be(success);
        await MockOidcClient.Received(2).RefreshAsync(RefreshToken, Arg.Any<CancellationToken>());
    }
}
