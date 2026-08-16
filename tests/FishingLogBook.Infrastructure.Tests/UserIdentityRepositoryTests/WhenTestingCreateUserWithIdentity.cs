using AwesomeAssertions;
using FishingLogBook.Infrastructure.Tests.TestSupport;
using FishingLogBook.Shared.Constants;

namespace FishingLogBook.Infrastructure.Tests.UserIdentityRepositoryTests;

[Collection(PostgresCollection.Name)]
public class WhenTestingCreateUserWithIdentity : BaseUserIdentityRepositoryTest
{
    public WhenTestingCreateUserWithIdentity(PostgresFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task ItShouldCreateAUserAndIdentityWhenTheSubjectIsNew()
    {
        // Arrange
        var (user, identity) = NewUserWithIdentity();
        var usersBefore = await CountUsersAsync();
        var identitiesBefore = await CountIdentitiesAsync();

        // Act
        var result = await Sut.CreateAsync(user, identity, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(user.Id);
        result.Value.Should().NotBe(Guid.Empty);
        var found = await Sut.FindUserIdAsync(Lookup(identity.Subject), CancellationToken.None);
        found.IsSuccess.Should().BeTrue();
        found.Value.Should().Be(user.Id);
        (await CountUsersAsync()).Should().Be(usersBefore + 1);
        (await CountIdentitiesAsync()).Should().Be(identitiesBefore + 1);
        (await CountIdentitiesAsync(IdentityProviderConstants.Cognito, identity.Subject)).Should().Be(1);
    }

    [Fact]
    public async Task ItShouldReuseTheExistingUserIdWhenTheSameIdentityIsCreatedAgain()
    {
        // Arrange
        var subject = NewSubject();
        var (firstUser, firstIdentity) = NewUserWithIdentity(subject: subject);
        var first = await Sut.CreateAsync(firstUser, firstIdentity, CancellationToken.None);
        var usersAfterFirst = await CountUsersAsync();
        var identitiesAfterFirst = await CountIdentitiesAsync();
        var (secondUser, secondIdentity) = NewUserWithIdentity(subject: subject);

        // Act
        var second = await Sut.CreateAsync(secondUser, secondIdentity, CancellationToken.None);

        // Assert
        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        second.Value.Should().Be(first.Value);
        second.Value.Should().Be(firstUser.Id);
        second.Value.Should().NotBe(secondUser.Id);
        second.Value.Should().NotBe(Guid.Empty);
        (await CountUsersAsync()).Should().Be(usersAfterFirst);
        (await CountIdentitiesAsync()).Should().Be(identitiesAfterFirst);
        (await CountUsersWithoutIdentityAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ItShouldCreateDifferentUsersWhenTheSubjectsDiffer()
    {
        // Arrange
        var (userA, identityA) = NewUserWithIdentity();
        var (userB, identityB) = NewUserWithIdentity();

        // Act
        var createdA = await Sut.CreateAsync(userA, identityA, CancellationToken.None);
        var createdB = await Sut.CreateAsync(userB, identityB, CancellationToken.None);

        // Assert
        createdA.Value.Should().Be(userA.Id);
        createdB.Value.Should().Be(userB.Id);
        createdA.Value.Should().NotBe(createdB.Value);
        createdA.Value.Should().NotBe(Guid.Empty);
        createdB.Value.Should().NotBe(Guid.Empty);
        (await CountIdentitiesAsync(IdentityProviderConstants.Cognito, identityA.Subject)).Should().Be(1);
        (await CountIdentitiesAsync(IdentityProviderConstants.Cognito, identityB.Subject)).Should().Be(1);
    }
}
