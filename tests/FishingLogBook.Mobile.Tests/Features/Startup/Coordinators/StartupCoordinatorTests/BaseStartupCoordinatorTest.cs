using System.Globalization;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Authentication.Services;
using FishingLogBook.Mobile.Features.OfflineAccess.Models;
using FishingLogBook.Mobile.Features.OfflineAccess.Services;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.Features.Startup.Coordinators;
using FishingLogBook.Mobile.Localization;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Startup.Coordinators.StartupCoordinatorTests;

public abstract class BaseStartupCoordinatorTest
{
    protected static readonly Guid UserId = Guid.Parse("0aa0a012-a3b1-46e6-976b-e65beccf6fc5");
    protected const string Subject = "cognito-subject";
    protected readonly IConnectivityService MockConnectivity = Substitute.For<IConnectivityService>();
    protected readonly IAuthenticationService MockAuthentication = Substitute.For<IAuthenticationService>();
    protected readonly IOfflineCapabilityService MockOfflineCapability = Substitute.For<IOfflineCapabilityService>();
    protected readonly IOfflinePreparationService MockOfflinePreparation = Substitute.For<IOfflinePreparationService>();
    protected readonly IDeviceUnlockService MockDeviceUnlock = Substitute.For<IDeviceUnlockService>();
    protected readonly IOfflineSessionService MockOfflineSession = Substitute.For<IOfflineSessionService>();
    protected readonly IAccountSessionCoordinator MockAccountSession = Substitute.For<IAccountSessionCoordinator>();
    protected readonly UiTextService Text = new(CultureInfo.GetCultureInfo("en-GB"));

    protected BaseStartupCoordinatorTest()
    {
        MockOfflinePreparation.IsPreparedAsync(
                Arg.Any<OfflineCapabilityModel>(),
                Arg.Any<CancellationToken>())
            .Returns(true);
    }

    protected StartupCoordinator CreateSut(TimeSpan? restorationTimeout = null)
    {
        return new StartupCoordinator(
            MockConnectivity,
            MockAuthentication,
            MockOfflineCapability,
            MockOfflinePreparation,
            MockDeviceUnlock,
            MockOfflineSession,
            MockAccountSession,
            Text,
            restorationTimeout ?? TimeSpan.FromSeconds(30));
    }
}
