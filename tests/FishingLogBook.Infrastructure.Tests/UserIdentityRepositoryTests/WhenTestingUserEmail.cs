using AwesomeAssertions;
using Dapper;
using FishingLogBook.Domain.Users;
using FishingLogBook.Infrastructure.Tests.TestSupport;
using FishingLogBook.Shared.Constants;
using Npgsql;

namespace FishingLogBook.Infrastructure.Tests.UserIdentityRepositoryTests;

[Collection(PostgresCollection.Name)]
public class WhenTestingUserEmail : BaseUserIdentityRepositoryTest
{
    public WhenTestingUserEmail(PostgresFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task ItShouldStoreEmailWhenTheUserIsCreated()
    {
        // Arrange
        const string email = "created@example.test";
        var (user, identity) = NewUserWithIdentity(email: email);

        // Act
        var created = await Sut.CreateAsync(user, identity, CancellationToken.None);

        // Assert
        created.IsSuccess.Should().BeTrue();
        created.Value.Should().Be(user.Id);
        user.Email.Should().Be(email);
        (await GetEmailAsync(user.Id)).Should().Be(email);
        (await CountIdentitiesAsync(IdentityProviderConstants.Cognito, identity.Subject)).Should().Be(1);
    }

    [Fact]
    public async Task ItShouldUpdateEmailWithoutChangingTheUserId()
    {
        // Arrange
        var (user, identity) = NewUserWithIdentity(email: "before@example.test");
        var created = await Sut.CreateAsync(user, identity, CancellationToken.None);
        var identitiesBefore = await CountIdentitiesAsync(IdentityProviderConstants.Cognito, identity.Subject);
        var updatedUser = new User { Id = user.Id, Email = "after@example.test" };

        // Act
        var updated = await Sut.UpdateEmailAsync(updatedUser, CancellationToken.None);
        var found = await Sut.FindUserIdAsync(Lookup(identity.Subject), CancellationToken.None);

        // Assert
        created.IsSuccess.Should().BeTrue();
        updated.IsSuccess.Should().BeTrue();
        found.Value.Should().Be(user.Id);
        (await GetEmailAsync(user.Id)).Should().Be("after@example.test");
        (await CountIdentitiesAsync(IdentityProviderConstants.Cognito, identity.Subject)).Should().Be(identitiesBefore);
    }

    [Fact]
    public async Task ItShouldCreateDifferentUsersWhenSubjectsShareAnEmail()
    {
        // Arrange
        const string email = "shared@example.test";
        var (userA, identityA) = NewUserWithIdentity(email: email);
        var (userB, identityB) = NewUserWithIdentity(email: email);

        // Act
        var createdA = await Sut.CreateAsync(userA, identityA, CancellationToken.None);
        var createdB = await Sut.CreateAsync(userB, identityB, CancellationToken.None);

        // Assert
        createdA.Value.Should().NotBe(createdB.Value);
        createdA.Value.Should().Be(userA.Id);
        createdB.Value.Should().Be(userB.Id);
        (await GetEmailAsync(userA.Id)).Should().Be(email);
        (await GetEmailAsync(userB.Id)).Should().Be(email);
        (await CountIdentitiesAsync(IdentityProviderConstants.Cognito, identityA.Subject)).Should().Be(1);
        (await CountIdentitiesAsync(IdentityProviderConstants.Cognito, identityB.Subject)).Should().Be(1);
    }

    [Fact]
    public async Task ItShouldAllowTheSameEmailOnDifferentUsers()
    {
        // Arrange
        const string email = "not-unique@example.test";
        var (userA, identityA) = NewUserWithIdentity(email: email);
        var (userB, identityB) = NewUserWithIdentity(email: email);

        // Act
        var first = await Sut.CreateAsync(userA, identityA, CancellationToken.None);
        var second = await Sut.CreateAsync(userB, identityB, CancellationToken.None);

        // Assert
        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        second.Value.Should().NotBe(first.Value);
        (await GetEmailAsync(userA.Id)).Should().Be(email);
        (await GetEmailAsync(userB.Id)).Should().Be(email);
        (await CountUsersWithoutIdentityAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ItShouldRejectAUserWithoutEmail()
    {
        // Arrange
        await using var connection = await ConnectionFactory.CreateOpenConnectionAsync(CancellationToken.None);
        var act = () => connection.ExecuteAsync(
            """INSERT INTO "User" ("Id") VALUES (@Id);""",
            new { Id = Guid.NewGuid() });

        // Act
        // Assert
        var exception = await act.Should().ThrowAsync<PostgresException>();
        exception.Which.SqlState.Should().Be(PostgresErrorCodes.NotNullViolation);
    }
}
