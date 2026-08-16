using System.Security.Claims;
using AwesomeAssertions;

namespace FishingLogBook.Web.Tests.Features.Authentication.Services.SignedInUserDisplayServiceTests;

public class WhenTestingGetEmail : BaseSignedInUserDisplayServiceTest
{
    [Fact]
    public void ItShouldReturnNullWhenTheUserIsUnauthenticated()
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var email = Sut.GetEmail(user);

        // Assert
        email.Should().BeNull();
    }

    [Fact]
    public void ItShouldReturnTheEmailClaim()
    {
        // Arrange
        var user = AuthenticatedUser(
            new Claim("name", "Eamonn Connolly"),
            new Claim("given_name", "Eamonn"),
            new Claim("family_name", "Connolly"),
            new Claim("email", "eamonn@example.test"));

        // Act
        var email = Sut.GetEmail(user);

        // Assert
        email.Should().Be("eamonn@example.test");
    }

    [Fact]
    public void ItShouldReturnNullWhenEmailIsMissing()
    {
        // Arrange
        var user = AuthenticatedUser(
            new Claim("sub", "cognito-sub-abc"),
            new Claim("preferred_username", "eamonn123"),
            new Claim("name", "Eamonn Connolly"));

        // Act
        var email = Sut.GetEmail(user);

        // Assert
        email.Should().BeNull();
    }

    [Fact]
    public void ItShouldIgnorePreferredUsernameAndSubject()
    {
        // Arrange
        var user = AuthenticatedUser(
            new Claim("sub", "cognito-sub-abc"),
            new Claim("preferred_username", "eamonn123"),
            new Claim("email", "eamonn@example.test"));

        // Act
        var email = Sut.GetEmail(user);

        // Assert
        email.Should().Be("eamonn@example.test");
        email.Should().NotBe("cognito-sub-abc");
        email.Should().NotBe("eamonn123");
    }

    private static ClaimsPrincipal AuthenticatedUser(params Claim[] claims)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"));
    }
}
