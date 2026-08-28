using AwesomeAssertions;
using FishingLogBook.Api.Tests.TestSupport;
using FishingLogBook.Tests.Common.TestSupport;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace FishingLogBook.Api.Tests.AuthenticationExtensionsTests;

public class WhenTestingAddFishingLogBookJwtBearer : BaseAuthenticationExtensionsTest
{
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
