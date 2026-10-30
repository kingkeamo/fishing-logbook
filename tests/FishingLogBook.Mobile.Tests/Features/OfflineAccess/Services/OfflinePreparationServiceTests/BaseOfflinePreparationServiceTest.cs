using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Authentication.Services;
using FishingLogBook.Mobile.Features.OfflineAccess.Models;
using FishingLogBook.Mobile.Features.OfflineAccess.Services;
using FishingLogBook.Mobile.Features.OfflineAccess.Stores;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.Features.Profile.Stores;
using FishingLogBook.Mobile.Tests.Support;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.OfflineAccess.Services.OfflinePreparationServiceTests;

public abstract class BaseOfflinePreparationServiceTest
{
    protected static readonly Guid UserId = Guid.Parse("8886b6e6-8327-46dc-a1f1-c2ffb463d929");
    protected const string Subject = "cognito-subject";
    protected readonly IAuthenticationService MockAuthentication = Substitute.For<IAuthenticationService>();
    protected readonly IAccountSessionCoordinator MockSessionCoordinator = Substitute.For<IAccountSessionCoordinator>();
    protected readonly IAnglerPreferencesService MockPreferencesService = Substitute.For<IAnglerPreferencesService>();
    protected readonly IAnglerPreferencesStore MockPreferencesStore = Substitute.For<IAnglerPreferencesStore>();
    protected readonly IOfflineAccountStore MockAccountStore = Substitute.For<IOfflineAccountStore>();
    protected readonly IOfflineCapabilityService MockCapabilityService = Substitute.For<IOfflineCapabilityService>();
    protected readonly AuthenticationSessionModel Session = new(
        "access-token",
        DateTimeOffset.UtcNow.AddHours(1),
        "refresh-token",
        Subject,
        "angler@example.com",
        UserId);
    protected readonly AccountSessionLeaseModel Lease = new(UserId, 1, CancellationToken.None);
    protected readonly AnglerPreferencesModel Preferences = TestAnglerPreferences.Create();
    protected readonly OfflinePreparationService Sut;

    protected BaseOfflinePreparationServiceTest()
    {
        MockAuthentication.CurrentSession.Returns(Session);
        MockSessionCoordinator.Capture().Returns(Lease);
        MockSessionCoordinator.IsCurrent(Lease).Returns(true);
        MockPreferencesService.RefreshAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<AnglerPreferencesModel>.Succeeded(Preferences));
        Sut = new OfflinePreparationService(
            MockAuthentication,
            MockSessionCoordinator,
            MockPreferencesService,
            MockPreferencesStore,
            MockAccountStore,
            MockCapabilityService,
            NullLogger<OfflinePreparationService>.Instance);
    }

    protected static ProfileDto Profile(Guid? userId = null, bool completed = true)
    {
        return new ProfileDto(
            userId ?? UserId,
            "Eamonn",
            null,
            null,
            null,
            "Connemara",
            true,
            false,
            true,
            true,
            true,
            WeightUnitEnum.Kg,
            LengthUnitEnum.Cm,
            completed);
    }

    protected OfflineCapabilityModel Capability()
    {
        return new OfflineCapabilityModel(Subject, UserId, IsDeviceReady: true);
    }

    protected OfflineAccountModel Account()
    {
        return new OfflineAccountModel(Subject, UserId, Session.Email, Profile());
    }

    protected void ArrangeSuccessfulPersistence()
    {
        MockCapabilityService.GetReadyAsync(Arg.Any<CancellationToken>())
            .Returns((OfflineCapabilityModel?)null, Capability());
        MockAccountStore.GetAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(Account(), Account());
        MockPreferencesStore.GetAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(Preferences, Preferences);
    }
}
