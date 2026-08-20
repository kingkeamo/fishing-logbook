using AwesomeAssertions;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;

namespace FishingLogBook.Infrastructure.Tests.Repositories.Repositories.UserIdentityRepositoryTests;

[Collection(PostgresCollection.Name)]
public class WhenTestingFindUserId : BaseUserIdentityRepositoryTest
{
    public WhenTestingFindUserId(PostgresFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task ItShouldReturnNullWhenNoMappingExists()
    {
        // Arrange
        var lookup = Lookup(NewSubject());

        // Act
        var found = await Sut.FindUserIdAsync(lookup, CancellationToken.None);

        // Assert
        found.IsSuccess.Should().BeTrue();
        found.Value.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldReturnTheUserIdWhenTheMappingExists()
    {
        // Arrange
        var (user, identity) = NewUserWithIdentity();
        var created = await Sut.CreateAsync(user, identity, CancellationToken.None);

        // Act
        var found = await Sut.FindUserIdAsync(Lookup(identity.Subject), CancellationToken.None);

        // Assert
        created.IsSuccess.Should().BeTrue();
        found.IsSuccess.Should().BeTrue();
        found.Value.Should().Be(user.Id);
        found.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task ItShouldLookupByProviderAndSubjectOnly()
    {
        // Arrange
        const string email = "shared@example.test";
        var (user, identity) = NewUserWithIdentity(email: email);
        await Sut.CreateAsync(user, identity, CancellationToken.None);
        var emailLookup = Lookup(email);

        // Act
        var bySubject = await Sut.FindUserIdAsync(Lookup(identity.Subject), CancellationToken.None);
        var byEmail = await Sut.FindUserIdAsync(emailLookup, CancellationToken.None);

        // Assert
        bySubject.Value.Should().Be(user.Id);
        byEmail.Value.Should().BeNull();
    }
}
