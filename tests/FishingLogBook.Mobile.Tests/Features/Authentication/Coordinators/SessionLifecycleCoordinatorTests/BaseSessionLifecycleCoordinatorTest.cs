using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Authentication.Services;
using FishingLogBook.Mobile.Features.OfflineAccess.Models;
using FishingLogBook.Mobile.Features.OfflineAccess.Services;
using FishingLogBook.Mobile.Features.Profile.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Authentication.Coordinators.SessionLifecycleCoordinatorTests;

public abstract class BaseSessionLifecycleCoordinatorTest
{
    protected static readonly Guid UserId = Guid.Parse("854d8b0a-1509-4905-b078-94367795a97a");
    protected static readonly DateTimeOffset Now = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);
    protected const string Subject = "cognito-subject";
    protected readonly IConnectivityService MockConnectivity = Substitute.For<IConnectivityService>();
    protected readonly IAuthenticationService MockAuthentication = Substitute.For<IAuthenticationService>();
    protected readonly IOfflineSessionService MockOfflineSession = Substitute.For<IOfflineSessionService>();
    protected readonly IAccountSessionCoordinator MockAccountSession = Substitute.For<IAccountSessionCoordinator>();
    protected readonly TimeProvider MockTimeProvider = Substitute.For<TimeProvider>();
    protected readonly OfflineCapabilityModel Capability = new(Subject, UserId, IsDeviceReady: true);
    protected readonly AccountSessionLeaseModel Lease = new(UserId, 4, CancellationToken.None);
    protected readonly SessionLifecycleCoordinator Sut;

    protected BaseSessionLifecycleCoordinatorTest()
    {
        MockConnectivity.IsOnline.Returns(true);
        MockOfflineSession.Current.Returns(Capability);
        MockAccountSession.Capture().Returns(Lease);
        MockAccountSession.IsCurrent(Lease).Returns(true);
        MockTimeProvider.GetUtcNow().Returns(Now);
        Sut = new SessionLifecycleCoordinator(
            MockConnectivity,
            MockAuthentication,
            MockOfflineSession,
            MockAccountSession,
            MockTimeProvider,
            TimeSpan.FromSeconds(30),
            NullLogger<SessionLifecycleCoordinator>.Instance);
    }

    protected static AuthenticationSessionModel OnlineSession(DateTimeOffset? expiresAt = null)
    {
        return new AuthenticationSessionModel(
            "access",
            expiresAt ?? Now.AddHours(1),
            "refresh",
            Subject,
            "angler@example.com",
            UserId);
    }
}
