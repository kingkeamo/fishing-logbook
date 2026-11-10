using System.Security.Claims;
using AwesomeAssertions;

namespace FishingLogBook.Web.Tests.Features.Authentication.Services.SignedInUserDisplayServiceTests;

public class WhenTestingEmailFallbacks : BaseSignedInUserDisplayServiceTest
{
    private static ClaimsPrincipal Authenticated(params Claim[] claims)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));
    }

    [Fact]
    public void ItShouldReturnNullWhenThereIsNoUser()
    {
        // Arrange
        // Act
        var email = Sut.GetEmail(null);

        // Assert
        email.Should().BeNull();
    }

    [Fact]
    public void ItShouldReturnNullForAnIdentityThatIsNotAuthenticated()
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("email", "angler@example.test")]));

        // Act
        var email = Sut.GetEmail(user);

        // Assert
        email.Should().BeNull();
    }

    [Fact]
    public void ItShouldTrimTheEmailClaim()
    {
        // Arrange
        var user = Authenticated(new Claim("email", "  angler@example.test  "));

        // Act
        var email = Sut.GetEmail(user);

        // Assert
        email.Should().Be("angler@example.test");
    }

    [Fact]
    public void ItShouldFallBackToTheStandardEmailClaimWhenTheShortClaimIsBlank()
    {
        // Arrange
        var user = Authenticated(
            new Claim("email", "   "),
            new Claim(ClaimTypes.Email, " fallback@example.test "));

        // Act
        var email = Sut.GetEmail(user);

        // Assert
        email.Should().Be("fallback@example.test");
    }

    [Fact]
    public void ItShouldUseTheStandardEmailClaimWhenTheShortClaimIsAbsent()
    {
        // Arrange
        var user = Authenticated(new Claim(ClaimTypes.Email, "standard@example.test"));

        // Act
        var email = Sut.GetEmail(user);

        // Assert
        email.Should().Be("standard@example.test");
    }

    [Fact]
    public void ItShouldReturnNullWhenBothEmailClaimsAreBlank()
    {
        // Arrange
        var user = Authenticated(new Claim("email", " "), new Claim(ClaimTypes.Email, string.Empty));

        // Act
        var email = Sut.GetEmail(user);

        // Assert
        email.Should().BeNull();
    }
}
