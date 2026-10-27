using FishingLogBook.Mobile.Features.Authentication.Browsers;
using FishingLogBook.Mobile.Features.Authentication.Clients;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Authentication.Enums;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Authentication.Services;
using FishingLogBook.Mobile.Features.Authentication.Stores;
using FishingLogBook.Mobile.Features.Users.Clients;
using FishingLogBook.Mobile.Tests.Support;
using FishingLogBook.Shared.Dtos;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Authentication.Services.AuthenticationServiceTests;

public abstract class BaseAuthenticationServiceTest
{
    protected static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
    protected static readonly Guid UserId = Guid.Parse("2fc39eab-07f5-4fb4-bb98-2b066a355da0");
    protected const string Subject = "cognito-subject";
    protected const string Email = "angler@example.com";
    protected const string AccessToken = "access-token";
    protected const string RefreshToken = "refresh-token";
    protected readonly IOidcProtocolClient MockOidcClient = Substitute.For<IOidcProtocolClient>();
    protected readonly IAuthenticationBrowser MockBrowser = Substitute.For<IAuthenticationBrowser>();
    protected readonly ISecureAuthenticationStore MockStore = Substitute.For<ISecureAuthenticationStore>();
    protected readonly ICurrentUserClient MockCurrentUserClient = Substitute.For<ICurrentUserClient>();
    protected readonly ICognitoSessionClient MockCognitoSessionClient = Substitute.For<ICognitoSessionClient>();
    protected readonly IAccountSessionCoordinator MockSessionCoordinator = Substitute.For<IAccountSessionCoordinator>();
    protected readonly AuthenticationService Sut;

    protected BaseAuthenticationServiceTest()
    {
        Sut = new AuthenticationService(
            MockOidcClient,
            MockBrowser,
            MockStore,
            MockCurrentUserClient,
            MockCognitoSessionClient,
            MockSessionCoordinator,
            new FixedTimeProvider(Now),
            NullLogger<AuthenticationService>.Instance);
    }

    protected PendingAuthorizationModel CreatePending(DateTimeOffset? expiresAt = null)
    {
        return new PendingAuthorizationModel(
            "https://fishing-logbook-dev.auth.eu-west-2.amazoncognito.com/oauth2/authorize",
            "state",
            "verifier",
            "com.fishinglogbook.mobile.demo://oauth2redirect",
            expiresAt ?? Now.AddMinutes(5));
    }

    protected OidcTokenResultModel CreateTokenResult(
        DateTimeOffset? expiresAt = null,
        string? refreshToken = RefreshToken)
    {
        return new OidcTokenResultModel(
            OidcFailureKindEnum.None,
            AccessToken,
            refreshToken,
            expiresAt ?? Now.AddHours(1),
            Subject,
            Email);
    }

    protected async Task EstablishSessionAsync(DateTimeOffset? expiresAt = null)
    {
        var pending = CreatePending();
        MockStore.ConsumePendingAuthorizationAsync().Returns(pending);
        MockOidcClient.CompleteLoginAsync(
                Arg.Any<Uri>(),
                pending,
                Arg.Any<CancellationToken>())
            .Returns(CreateTokenResult(expiresAt));
        MockCurrentUserClient.GetCurrentAsync(AccessToken, Arg.Any<CancellationToken>())
            .Returns(new CurrentUserDto(UserId, Email, "Cognito", Subject));
        await Sut.CompleteSignInAsync(
            new Uri("com.fishinglogbook.mobile.demo://oauth2redirect?code=code&state=state"),
            CancellationToken.None);
    }
}
