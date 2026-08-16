using AwesomeAssertions;
using Dapper;
using FishingLogBook.Infrastructure.Tests.TestSupport;
using FishingLogBook.Shared.Constants;
using Npgsql;

namespace FishingLogBook.Infrastructure.Tests.UserIdentityRepositoryTests;

[Collection(PostgresCollection.Name)]
public class WhenTestingUniqueProviderSubject : BaseUserIdentityRepositoryTest
{
    public WhenTestingUniqueProviderSubject(PostgresFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task ItShouldRejectADuplicateProviderAndSubject()
    {
        // Arrange
        var (user, identity) = NewUserWithIdentity();
        var created = await Sut.CreateAsync(user, identity, CancellationToken.None);
        created.IsSuccess.Should().BeTrue();
        await using var connection = await ConnectionFactory.CreateOpenConnectionAsync(CancellationToken.None);

        var act = () => connection.ExecuteAsync(
            """
            INSERT INTO "UserIdentity" ("Id", "UserId", "Provider", "Subject")
            VALUES (@Id, @UserId, @Provider, @Subject);
            """,
            new
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Provider = IdentityProviderConstants.Cognito,
                Subject = identity.Subject
            });

        // Act
        // Assert
        var exception = await act.Should().ThrowAsync<PostgresException>();
        exception.Which.SqlState.Should().Be(PostgresErrorCodes.UniqueViolation);
        (await CountIdentitiesAsync(IdentityProviderConstants.Cognito, identity.Subject)).Should().Be(1);
        var found = await Sut.FindUserIdAsync(Lookup(identity.Subject), CancellationToken.None);
        found.Value.Should().Be(user.Id);
    }
}
