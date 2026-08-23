using AwesomeAssertions;
using FishingLogBook.Application.Contracts.Services;

namespace FishingLogBook.Application.Tests.Users.CurrentUserTests;

public class WhenTestingAssign : BaseCurrentUserTest
{
    [Fact]
    public void ItShouldRejectAnEmptyUserId()
    {
        // Arrange
        var act = () => Sut.Assign(Guid.Empty, "eamonn@example.test", "Cognito", "subject");

        // Act
        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("FishingLogBook UserId cannot be empty.");
        Sut.IsResolved.Should().BeFalse();
        Sut.UserId.Should().Be(Guid.Empty);
        Sut.Email.Should().BeEmpty();
    }

    [Fact]
    public void ItShouldRejectAMissingEmail()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var act = () => Sut.Assign(userId, "  ", "Cognito", "subject");

        // Act
        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Authenticated email is missing.");
        Sut.IsResolved.Should().BeFalse();
        Sut.UserId.Should().Be(Guid.Empty);
        Sut.Email.Should().BeEmpty();
    }

    [Fact]
    public void ItShouldExposeTheAssignedUserIdAndEmail()
    {
        // Arrange
        var userId = Guid.NewGuid();
        const string email = "eamonn@example.test";

        // Act
        Sut.Assign(userId, email, "Cognito", "subject");

        // Assert
        Sut.IsResolved.Should().BeTrue();
        Sut.UserId.Should().Be(userId);
        Sut.UserId.Should().NotBe(Guid.Empty);
        Sut.Email.Should().Be(email);
        Sut.Provider.Should().Be("Cognito");
        Sut.Subject.Should().Be("subject");
        typeof(ICurrentUser).GetProperty(nameof(ICurrentUser.Email)).Should().NotBeNull();
    }

    [Theory]
    [InlineData("", "subject")]
    [InlineData("Cognito", "")]
    public void ItShouldRejectMissingTrustedIdentity(string provider, string subject)
    {
        var act = () => Sut.Assign(Guid.NewGuid(), "eamonn@example.test", provider, subject);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Authenticated identity is missing.");
        Sut.IsResolved.Should().BeFalse();
    }
}
