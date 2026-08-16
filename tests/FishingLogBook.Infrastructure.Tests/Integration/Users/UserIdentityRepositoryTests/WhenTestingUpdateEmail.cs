using AwesomeAssertions;
using FishingLogBook.Domain.Users;
using FishingLogBook.Infrastructure.Tests.Integration.TestSupport;
using FishingLogBook.Shared.Constants;

namespace FishingLogBook.Infrastructure.Tests.Integration.Users.UserIdentityRepositoryTests;

[Collection(PostgresCollection.Name)]
public class WhenTestingUpdateEmail : BaseUserIdentityRepositoryTest
{
    public WhenTestingUpdateEmail(PostgresFixture fixture)
        : base(fixture)
    {
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
}
