using AwesomeAssertions;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Application.Users;

namespace FishingLogBook.Application.Tests.Users.CurrentUserTests;

public class WhenTestingAssign
{
    [Fact]
    public void ItShouldExposeTheAssignedUserIdAndEmail()
    {
        // Arrange
        var sut = new CurrentUser();
        var userId = Guid.NewGuid();
        const string email = "eamonn@example.test";

        // Act
        sut.Assign(userId, email);

        // Assert
        sut.IsResolved.Should().BeTrue();
        sut.UserId.Should().Be(userId);
        sut.UserId.Should().NotBe(Guid.Empty);
        sut.Email.Should().Be(email);
        typeof(ICurrentUser).GetProperty(nameof(ICurrentUser.Email)).Should().NotBeNull();
    }

    [Fact]
    public void ItShouldRejectAnEmptyUserId()
    {
        // Arrange
        var sut = new CurrentUser();
        var act = () => sut.Assign(Guid.Empty, "eamonn@example.test");

        // Act
        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("FishingLogBook UserId cannot be empty.");
        sut.IsResolved.Should().BeFalse();
        sut.UserId.Should().Be(Guid.Empty);
        sut.Email.Should().BeEmpty();
    }

    [Fact]
    public void ItShouldRejectAMissingEmail()
    {
        // Arrange
        var sut = new CurrentUser();
        var userId = Guid.NewGuid();
        var act = () => sut.Assign(userId, "  ");

        // Act
        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Authenticated email is missing.");
        sut.IsResolved.Should().BeFalse();
        sut.UserId.Should().Be(Guid.Empty);
        sut.Email.Should().BeEmpty();
    }
}
