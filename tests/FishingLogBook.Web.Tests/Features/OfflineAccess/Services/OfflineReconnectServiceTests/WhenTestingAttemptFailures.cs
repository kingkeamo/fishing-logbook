using AwesomeAssertions;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.OfflineAccess.Enums;
using Microsoft.AspNetCore.Components.Authorization;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.OfflineAccess.Services.OfflineReconnectServiceTests;

public class WhenTestingAttemptFailures : BaseOfflineReconnectServiceTest
{
    [Fact]
    public async Task ItShouldStayOfflineWhenNoOfflineOwnerIsUnlocked()
    {
        // Arrange
        OfflineOwnerContext.Lock();

        // Act
        await Sut.AttemptAsync(CancellationToken.None);

        // Assert
        Sut.State.Should().Be(OfflineReconnectStateEnum.Offline);
        await MockAuthenticationStateProvider.DidNotReceive().GetAuthenticationStateAsync();
        await MockCurrentUserClient.DidNotReceive().GetCurrentAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldExposeARetryableFailureAndLogWhenAuthenticationCannotBeRead()
    {
        // Arrange
        var failure = new InvalidOperationException("auth provider failed");
        MockAuthenticationStateProvider.GetAuthenticationStateAsync()
            .Returns(Task.FromException<AuthenticationState>(failure));

        // Act
        await Sut.AttemptAsync(CancellationToken.None);

        // Assert
        Sut.State.Should().Be(OfflineReconnectStateEnum.RetryableFailure);
        OfflineOwnerContext.IsUnlocked.Should().BeTrue();
        await MockLoggingService.Received(1).LogErrorAsync(
            "offline reconnect authentication",
            failure,
            CancellationToken.None);
        await MockCurrentUserClient.DidNotReceive().GetCurrentAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldTreatAnUnresolvedAuthenticatedUserAsARetryableFailure()
    {
        // Arrange
        MockCurrentUserClient.GetCurrentAsync(Arg.Any<CancellationToken>())
            .Returns(new CurrentUserDto(Guid.Empty, "owner@example.test", "Cognito", "owner-subject"));

        // Act
        await Sut.AttemptAsync(CancellationToken.None);

        // Assert
        Sut.State.Should().Be(OfflineReconnectStateEnum.RetryableFailure);
        OfflineOwnerContext.IsUnlocked.Should().BeTrue();
        await MockLogbookSynchroniser.DidNotReceive().SynchronisePendingAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
        await MockLoggingService.Received(1).LogErrorAsync(
            "offline reconnect owner verification",
            Arg.Is<Exception>(exception => exception is InvalidOperationException),
            CancellationToken.None);
    }

    [Fact]
    public async Task ItShouldReportOnlineAndLockTheOwnerAfterAMatchingOwnerSynchronises()
    {
        // Arrange
        // Act
        await Sut.AttemptAsync(CancellationToken.None);

        // Assert
        Sut.State.Should().Be(OfflineReconnectStateEnum.Online);
        await MockLogbookSynchroniser.Received(1).SynchronisePendingAsync(
            OfflineUserId,
            Arg.Any<CancellationToken>());
    }
}
