using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using FishingLogBook.Api.Tests.TestSupport;
using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Users;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Tests.Common.TestSupport;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using NSubstitute;

namespace FishingLogBook.Api.Tests.AuthenticationExtensionsTests;

public class WhenTestingAddFishingLogBookJwtBearer : BaseAuthenticationExtensionsTest
{
    public static TheoryData<string, string> InvalidAccessTokens
    {
        get
        {
            return new TheoryData<string, string>
            {
                { "wrong issuer", TestJwt.CreateAccessToken(issuer: "https://cognito-idp.us-east-1.amazonaws.com/us-east-1_otherpool") },
                { "untrusted signature", TestJwt.CreateAccessToken(useUntrustedSigningKey: true) },
                { "expired", TestJwt.CreateAccessToken(expires: DateTime.UtcNow.AddMinutes(-2)) },
                { "missing audience", TestJwt.CreateAccessToken(includeAudience: false) },
                { "wrong audience", TestJwt.CreateAccessToken(audience: "https://unapproved-api.example.test") },
                { "ID token use", TestJwt.CreateAccessToken(tokenUse: "id") },
                { "missing required scope", TestJwt.CreateAccessToken(scope: "openid email profile") },
                { "missing client ID", TestJwt.CreateAccessToken(includeClientId: false) },
                { "arbitrary pool client", TestJwt.CreateAccessToken(clientId: TestJwt.ArbitraryClientId) }
            };
        }
    }

    [Theory]
    [MemberData(nameof(InvalidAccessTokens))]
    public async Task ItShouldRejectAnAccessTokenWhenASecurityClaimIsInvalid(string scenario, string token)
    {
        // Arrange
        using var factory = new SystemApiFactory();
        var client = factory.CreateAuthenticatedClient(token);

        // Act
        var response = await client.GetAsync("/api/users/current");

        // Assert
        response.StatusCode.Should().Be(
            HttpStatusCode.Unauthorized,
            "the {0} token must be rejected",
            scenario);
        await factory.UserIdentityRepository.DidNotReceive().FindUserIdAsync(
            Arg.Any<FindUserIdentityArgs>(),
            Arg.Any<CancellationToken>());
        await factory.UserIdentityRepository.DidNotReceive().CreateAsync(
            Arg.Any<User>(),
            Arg.Any<UserIdentity>(),
            Arg.Any<CancellationToken>());
        await factory.UserIdentityRepository.DidNotReceive().UpdateEmailAsync(
            Arg.Any<User>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(TestJwt.ClientId, "web-client")]
    [InlineData(TestJwt.NativeClientId, "native-client")]
    public async Task ItShouldAcceptAnAccessTokenFromEachApprovedClient(string clientId, string subject)
    {
        // Arrange
        using var factory = new SystemApiFactory();
        var client = factory.CreateAuthenticatedClient(
            TestJwt.CreateAccessToken(clientId: clientId, subject: subject));

        // Act
        var response = await client.GetAsync("/api/users/current");
        var body = await response.Content.ReadFromJsonAsync<CurrentUserDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotBeNull();
        body!.UserId.Should().NotBe(Guid.Empty);
        body.Email.Should().Be(TestJwt.Email);
        body.Provider.Should().Be(IdentityProviderConstants.Cognito);
        body.Subject.Should().Be(subject);
        await factory.UserIdentityRepository.Received(1).FindUserIdAsync(
            Arg.Is<FindUserIdentityArgs>(args =>
                args.Provider == IdentityProviderConstants.Cognito &&
                args.Subject == subject),
            Arg.Any<CancellationToken>());
        await factory.UserIdentityRepository.Received(1).CreateAsync(
            Arg.Is<User>(user =>
                user.Id != Guid.Empty &&
                user.Email == TestJwt.Email),
            Arg.Is<UserIdentity>(identity =>
                identity.UserId != Guid.Empty &&
                identity.Provider == IdentityProviderConstants.Cognito &&
                identity.Subject == subject),
            Arg.Any<CancellationToken>());
        await factory.UserIdentityRepository.DidNotReceive().UpdateEmailAsync(
            Arg.Any<User>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ItShouldConfigureASocketsHttpHandlerConnectCallback()
    {
        // Arrange
        var options = CreateConfiguredJwtBearerOptions();

        // Act
        var handler = options.BackchannelHttpHandler as SocketsHttpHandler;

        // Assert
        handler.Should().NotBeNull();
        handler!.ConnectCallback.Should().NotBeNull();
    }

    [Fact]
    public void ItShouldKeepTheJwtTokenValidationParameters()
    {
        // Arrange
        var options = CreateConfiguredJwtBearerOptions();

        // Act
        var parameters = options.TokenValidationParameters;

        // Assert
        options.Authority.Should().Be(TestJwt.Issuer);
        options.RequireHttpsMetadata.Should().BeTrue();
        options.MetadataAddress.Should().Be(
            $"{TestJwt.Issuer.TrimEnd('/')}/.well-known/openid-configuration");
        options.BackchannelTimeout.Should().Be(TimeSpan.FromMinutes(1));
        options.MapInboundClaims.Should().BeFalse();
        options.IncludeErrorDetails.Should().BeFalse();
        parameters.ValidateIssuer.Should().BeTrue();
        parameters.ValidIssuer.Should().Be(TestJwt.Issuer);
        parameters.ValidateAudience.Should().BeTrue();
        parameters.ValidAudience.Should().Be(TestAuthConstants.ApiResource);
        parameters.ValidateLifetime.Should().BeTrue();
        parameters.ValidateIssuerSigningKey.Should().BeTrue();
        parameters.ClockSkew.Should().Be(TimeSpan.FromSeconds(30));
    }
}
