using System.Net;
using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Authentication.Enums;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.OfflineAccess.Enums;
using FishingLogBook.Mobile.Features.OfflineAccess.Models;
using FishingLogBook.Mobile.Features.Startup.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Startup.Coordinators.StartupCoordinatorTests.Restoration;

public sealed class WhenTestingStart : BaseStartupCoordinatorTest
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ItShouldOpenOnlyThePreparedLocalOwnerWithoutIssuingAnApiToken(bool isOnline)
    {
        // Arrange
        MockConnectivity.IsOnline.Returns(isOnline);

        // Act
        var result = await Sut.StartAsync(CancellationToken.None);
        var getToken = () => Authentication.GetAccessTokenAsync(CancellationToken.None);

        // Assert
        result.Should().Be(StartupResultEnum.OfflineSessionReady);
        OfflineSession.Current.Should().Be(PersistedCapability);
        AccountSession.Capture()!.UserId.Should().Be(UserId);
        await getToken.Should().ThrowAsync<UnauthorizedAccessException>();
        await MockUnlock.Received(1).UnlockAsync(Text["OfflineUnlockReason"], CancellationToken.None);
        await MockRefresh.Received(isOnline ? 1 : 0).RefreshAsync(RefreshToken, Arg.Any<CancellationToken>());
        await MockAuthenticationStore.DidNotReceive().SaveCredentialAsync(Arg.Any<PersistedAuthenticationModel>());
    }

    [Fact]
    public async Task ItShouldUnlockThePreparedOwnerAfterABoundedAuthenticationTimeout()
    {
        // Arrange
        MockRefresh.RefreshAsync(RefreshToken, Arg.Any<CancellationToken>()).Returns(async call =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, call.Arg<CancellationToken>());
            return SuccessfulRefresh();
        });

        // Act
        var result = await Sut.StartAsync(CancellationToken.None);

        // Assert
        result.Should().Be(StartupResultEnum.OfflineSessionReady);
        OfflineSession.Current.Should().Be(PersistedCapability);
        Authentication.CurrentSession.Should().BeNull();
        await MockRefresh.Received(1).RefreshAsync(RefreshToken, Arg.Any<CancellationToken>());
        await MockUnlock.Received(1).UnlockAsync(Text["OfflineUnlockReason"], CancellationToken.None);
    }

    [Theory]
    [InlineData(false, StartupResultEnum.OfflineAccessUnavailable)]
    [InlineData(true, StartupResultEnum.SignInRequired)]
    public async Task ItShouldDenyOfflineEntryAfterExplicitSignOut(bool isOnline, StartupResultEnum expected)
    {
        // Arrange
        await Authentication.SignOutAsync(CancellationToken.None);
        MockConnectivity.IsOnline.Returns(isOnline);

        // Act
        var result = await Sut.StartAsync(CancellationToken.None);

        // Assert
        result.Should().Be(expected);
        PersistedCapability.Should().BeNull();
        PersistedCredential.Should().BeNull();
        OfflineSession.Current.Should().BeNull();
        AccountSession.Capture().Should().BeNull();
        await MockUnlock.DidNotReceive().UnlockAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await MockRefresh.DidNotReceive().RefreshAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(OidcFailureKindEnum.InvalidGrant)]
    [InlineData(OidcFailureKindEnum.Rejected)]
    [InlineData(OidcFailureKindEnum.InvalidResponse)]
    public async Task ItShouldRevokeOfflineReadinessWhenAuthenticationIsExplicitlyRejected(OidcFailureKindEnum failure)
    {
        // Arrange
        MockRefresh.RefreshAsync(RefreshToken, Arg.Any<CancellationToken>()).Returns(OidcTokenResultModel.Failed(failure));

        // Act
        var result = await Sut.StartAsync(CancellationToken.None);

        // Assert
        result.Should().Be(StartupResultEnum.SignInRequired);
        PersistedCapability.Should().BeNull();
        PersistedCredential.Should().BeNull();
        OfflineSession.Current.Should().BeNull();
        await MockUnlock.DidNotReceive().UnlockAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable, StartupResultEnum.OfflineSessionReady)]
    [InlineData(HttpStatusCode.RequestTimeout, StartupResultEnum.OfflineSessionReady)]
    [InlineData(HttpStatusCode.Unauthorized, StartupResultEnum.SignInRequired)]
    [InlineData(HttpStatusCode.Forbidden, StartupResultEnum.SignInRequired)]
    [InlineData(HttpStatusCode.BadRequest, StartupResultEnum.ServiceUnavailable)]
    public async Task ItShouldDistinguishBackendOutagesFromExplicitRejection(HttpStatusCode status, StartupResultEnum expected)
    {
        // Arrange
        IdentityStatus = status;
        MockRefresh.RefreshAsync(RefreshToken, Arg.Any<CancellationToken>()).Returns(SuccessfulRefresh());

        // Act
        var result = await Sut.StartAsync(CancellationToken.None);

        // Assert
        result.Should().Be(expected);
        Authentication.CurrentSession.Should().BeNull();
        if (expected == StartupResultEnum.OfflineSessionReady)
        {
            OfflineSession.Current.Should().Be(PersistedCapability);
            AccountSession.Capture()!.UserId.Should().Be(UserId);
            await MockUnlock.Received(1).UnlockAsync(Text["OfflineUnlockReason"], CancellationToken.None);
        }
        else
        {
            OfflineSession.Current.Should().BeNull();
            await MockUnlock.DidNotReceive().UnlockAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }
        await MockRefresh.Received(1).RefreshAsync(RefreshToken, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotReviveAnAccountSignedOutDuringDeviceUnlock()
    {
        // Arrange
        MockUnlock.UnlockAsync(Arg.Any<string>(), CancellationToken.None).Returns(async _ =>
        {
            await Authentication.SignOutAsync(CancellationToken.None);
            return DeviceUnlockResultEnum.Success;
        });

        // Act
        var result = await Sut.StartAsync(CancellationToken.None);

        // Assert
        result.Should().Be(StartupResultEnum.OfflineAccessUnavailable);
        OfflineSession.Current.Should().BeNull();
        AccountSession.Capture().Should().BeNull();
        PersistedCapability.Should().BeNull();
        await MockUnlock.Received(1).UnlockAsync(Text["OfflineUnlockReason"], CancellationToken.None);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ItShouldFailClosedForDisabledReadinessOrMissingPreparedData(bool missingData)
    {
        // Arrange
        if (missingData)
        {
            MockAccountStore.GetAsync(UserId, Arg.Any<CancellationToken>()).Returns((OfflineAccountModel?)null);
        }
        else
        {
            PersistedCapability = PersistedCapability! with { IsDeviceReady = false };
        }

        // Act
        var result = await Sut.StartAsync(CancellationToken.None);

        // Assert
        result.Should().Be(StartupResultEnum.OfflineAccessUnavailable);
        OfflineSession.Current.Should().BeNull();
        AccountSession.Capture().Should().BeNull();
        await MockUnlock.DidNotReceive().UnlockAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRecoverTheSameOfflineOwnerAndEnableSynchronisationWithoutRewritingLocalData()
    {
        // Arrange
        var startup = await Sut.StartAsync(CancellationToken.None);
        var lease = AccountSession.Capture();
        var originalCapability = PersistedCapability;
        using var lifecycle = new SessionLifecycleCoordinator(
            MockConnectivity, Authentication, OfflineSession, AccountSession, MockTime,
            TimeSpan.FromSeconds(1), NullLogger<SessionLifecycleCoordinator>.Instance);
        lifecycle.Start();
        MockTime.GetUtcNow().Returns(Now.AddSeconds(16));
        MockRefresh.RefreshAsync(RefreshToken, Arg.Any<CancellationToken>()).Returns(SuccessfulRefresh());

        // Act
        MockConnectivity.IsOnline.Returns(false);
        MockConnectivity.ConnectivityChanged += Raise.Event<EventHandler>(MockConnectivity, EventArgs.Empty);
        MockConnectivity.IsOnline.Returns(true);
        MockConnectivity.ConnectivityChanged += Raise.Event<EventHandler>(MockConnectivity, EventArgs.Empty);
        await lifecycle.ForegroundAsync(CancellationToken.None);

        // Assert
        startup.Should().Be(StartupResultEnum.OfflineSessionReady);
        lifecycle.State.Should().Be(SessionLifecycleStateEnum.Online);
        Authentication.CurrentSession!.UserId.Should().Be(UserId);
        Authentication.CurrentSession.Subject.Should().Be(Subject);
        OfflineSession.Current.Should().BeNull();
        AccountSession.IsCurrent(lease!).Should().BeTrue();
        PersistedCapability.Should().Be(originalCapability);
        (await MockAccountStore.GetAsync(UserId, CancellationToken.None)).Should().Be(Account);
        await MockAccountStore.DidNotReceive().SaveAsync(Arg.Any<OfflineAccountModel>(), Arg.Any<CancellationToken>());
        await MockRefresh.Received(2).RefreshAsync(RefreshToken, Arg.Any<CancellationToken>());
    }
}
