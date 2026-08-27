using System.Security.Claims;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Browser.Network;
using FishingLogBook.Web.Common.Offline.Synchronisers;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.OfflineAccess.Models;
using FishingLogBook.Web.Features.OfflineAccess.Services;
using FishingLogBook.Web.Features.Users.Clients;
using Microsoft.AspNetCore.Components.Authorization;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.OfflineAccess.Services.OfflineReconnectServiceTests;

public class BaseOfflineReconnectServiceTest
{
    protected static readonly Guid OfflineUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    protected readonly AuthenticationStateProvider MockAuthenticationStateProvider =
        Substitute.For<AuthenticationStateProvider>();
    protected readonly ICurrentUserClient MockCurrentUserClient = Substitute.For<ICurrentUserClient>();
    protected readonly ILogbookSynchroniser MockLogbookSynchroniser = Substitute.For<ILogbookSynchroniser>();
    protected readonly ILoggingService MockLoggingService = Substitute.For<ILoggingService>();
    protected readonly INetworkService MockNetworkService = Substitute.For<INetworkService>();
    protected readonly OfflineOwnerContextService OfflineOwnerContext = new();
    protected readonly OfflineReconnectService Sut;

    protected BaseOfflineReconnectServiceTest()
    {
        OfflineOwnerContext.Unlock(new OfflineOwnerModel(OfflineUserId, 1));
        MockAuthenticationStateProvider.GetAuthenticationStateAsync().Returns(Authenticated());
        MockCurrentUserClient.GetCurrentAsync(Arg.Any<CancellationToken>())
            .Returns(CurrentUser(OfflineUserId));
        Sut = new OfflineReconnectService(
            MockAuthenticationStateProvider,
            MockCurrentUserClient,
            MockLogbookSynchroniser,
            OfflineOwnerContext,
            MockLoggingService,
            MockNetworkService);
    }

    protected static AuthenticationState Authenticated()
    {
        var identity = new ClaimsIdentity(
            [new Claim("sub", "owner-subject")],
            "test");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    protected static AuthenticationState Anonymous()
    {
        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
    }

    protected static CurrentUserDto CurrentUser(Guid userId)
    {
        return new CurrentUserDto(userId, "owner@example.test", "Cognito", "owner-subject");
    }
}
