using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using FishingLogBook.Mobile.Features.Authentication.Browsers;
using FishingLogBook.Mobile.Features.Authentication.Clients;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Authentication.Enums;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Authentication.Services;
using FishingLogBook.Mobile.Features.Authentication.Stores;
using FishingLogBook.Mobile.Features.OfflineAccess.Enums;
using FishingLogBook.Mobile.Features.OfflineAccess.Models;
using FishingLogBook.Mobile.Features.OfflineAccess.Services;
using FishingLogBook.Mobile.Features.OfflineAccess.Stores;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.Features.Profile.Stores;
using FishingLogBook.Mobile.Features.Startup.Coordinators;
using FishingLogBook.Mobile.Features.Users.Clients;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Mobile.Tests.Support;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Startup.Coordinators.StartupCoordinatorTests.Restoration;

public abstract class BaseStartupCoordinatorTest : IDisposable
{
    protected static readonly Guid UserId = Guid.Parse("19b828d3-09df-44c2-b881-9e0ced01e369");
    protected static readonly DateTimeOffset Now = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);
    protected const string Subject = "prepared-subject";
    protected const string RefreshToken = "test-refresh";
    protected const string AccessToken = "test-access";
    protected readonly IConnectivityService MockConnectivity = Substitute.For<IConnectivityService>();
    protected readonly ISecureAuthenticationStore MockAuthenticationStore = Substitute.For<ISecureAuthenticationStore>();
    protected readonly ISecureOfflineCapabilityStore MockCapabilityStore = Substitute.For<ISecureOfflineCapabilityStore>();
    protected readonly IOidcRefreshService MockRefresh = Substitute.For<IOidcRefreshService>();
    protected readonly IDeviceUnlockService MockUnlock = Substitute.For<IDeviceUnlockService>();
    protected readonly IOfflineAccountStore MockAccountStore = Substitute.For<IOfflineAccountStore>();
    protected readonly IAnglerPreferencesStore MockPreferencesStore = Substitute.For<IAnglerPreferencesStore>();
    protected readonly TimeProvider MockTime = Substitute.For<TimeProvider>();
    protected readonly UiTextService Text = new(CultureInfo.GetCultureInfo("en-GB"));
    protected readonly AccountSessionCoordinator AccountSession = new();
    protected readonly OfflineSessionService OfflineSession = new();
    protected readonly OfflineCapabilityService Capability;
    protected readonly AuthenticationService Authentication;
    protected readonly StartupCoordinator Sut;
    protected readonly OfflineAccountModel Account;
    protected OfflineCapabilityModel? PersistedCapability = new(Subject, UserId, IsDeviceReady: true);
    protected PersistedAuthenticationModel? PersistedCredential = new(RefreshToken, Subject, UserId);
    protected HttpStatusCode IdentityStatus = HttpStatusCode.OK;
    private readonly HttpClient _http;

    protected BaseStartupCoordinatorTest()
    {
        MockTime.GetUtcNow().Returns(Now);
        MockConnectivity.IsOnline.Returns(true);
        MockConnectivity.TryRunWhileOnline(Arg.Any<Action>()).Returns(call =>
        {
            if (!MockConnectivity.IsOnline)
            {
                return false;
            }

            call.Arg<Action>()();
            return true;
        });
        MockAuthenticationStore.GetCredentialAsync().Returns(_ => PersistedCredential);
        MockAuthenticationStore.When(store => store.RemoveCredentialAsync()).Do(_ => PersistedCredential = null);
        MockAuthenticationStore.When(store => store.SaveCredentialAsync(Arg.Any<PersistedAuthenticationModel>()))
            .Do(call => PersistedCredential = call.Arg<PersistedAuthenticationModel>());
        MockCapabilityStore.GetAsync(Arg.Any<CancellationToken>()).Returns(_ => PersistedCapability);
        MockCapabilityStore.When(store => store.RemoveAsync(Arg.Any<CancellationToken>())).Do(_ => PersistedCapability = null);
        MockCapabilityStore.When(store => store.SaveAsync(Arg.Any<OfflineCapabilityModel>(), Arg.Any<CancellationToken>()))
            .Do(call => PersistedCapability = call.Arg<OfflineCapabilityModel>());
        Capability = new OfflineCapabilityService(MockCapabilityStore);
        _http = new HttpClient(new RecordingHttpMessageHandler(_ => new HttpResponseMessage(IdentityStatus)
        {
            Content = JsonContent.Create(new CurrentUserDto(UserId, "angler@example.com", "Cognito", Subject))
        }))
        {
            BaseAddress = new Uri("https://api.example.com/")
        };
        Authentication = new AuthenticationService(
            Substitute.For<IOidcProtocolClient>(), MockRefresh, Substitute.For<IAuthenticationBrowser>(),
            MockAuthenticationStore, new CurrentUserClient(_http), Substitute.For<ICognitoSessionClient>(),
            AccountSession, Capability, OfflineSession, MockConnectivity, MockTime, NullLogger<AuthenticationService>.Instance);
        Account = new OfflineAccountModel(Subject, UserId, "angler@example.com", new ProfileDto(
            UserId, "Angler", null, null, null, "Lake", true, false, true, true, true,
            WeightUnitEnum.Kg, LengthUnitEnum.Cm, true));
        MockAccountStore.GetAsync(UserId, Arg.Any<CancellationToken>()).Returns(Account);
        MockPreferencesStore.GetAsync(UserId, Arg.Any<CancellationToken>()).Returns(TestAnglerPreferences.Create());
        var preparation = new OfflinePreparationService(
            Authentication, AccountSession, Substitute.For<IAnglerPreferencesService>(), MockPreferencesStore,
            MockAccountStore, Capability, NullLogger<OfflinePreparationService>.Instance);
        MockUnlock.UnlockAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(DeviceUnlockResultEnum.Success);
        MockRefresh.RefreshAsync(RefreshToken, Arg.Any<CancellationToken>())
            .Returns(OidcTokenResultModel.Failed(OidcFailureKindEnum.Transient));
        Sut = new StartupCoordinator(
            MockConnectivity, Authentication, Capability, preparation, MockUnlock, OfflineSession, AccountSession,
            Text, TimeSpan.FromSeconds(1));
    }

    public void Dispose()
    {
        Authentication.Dispose();
        AccountSession.Dispose();
        _http.Dispose();
    }

    protected OidcTokenResultModel SuccessfulRefresh()
    {
        return new OidcTokenResultModel(OidcFailureKindEnum.None, AccessToken, RefreshToken, Now.AddHours(1), Subject, "angler@example.com");
    }
}
