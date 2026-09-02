using AwesomeAssertions;
using Dapper;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Users.Services;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;
using FishingLogBook.Shared.Constants;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace FishingLogBook.Infrastructure.Tests.Repositories.Repositories.UserIdentityRepositoryTests;

[Collection(PostgresCollection.Name)]
public class WhenTestingCreate : BaseUserIdentityRepositoryTest
{
    public WhenTestingCreate(PostgresFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task ItShouldRejectAUserWithoutEmail()
    {
        // Arrange
        await using var connection = await ConnectionFactory.CreateOpenConnectionAsync(CancellationToken.None);
        var act = () => connection.ExecuteAsync(
            """INSERT INTO users (id) VALUES (@Id);""",
            new { Id = Guid.NewGuid() });

        // Act
        // Assert
        var exception = await act.Should().ThrowAsync<PostgresException>();
        exception.Which.SqlState.Should().Be(PostgresErrorCodes.NotNullViolation);
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
            INSERT INTO useridentities (id, userid, provider, subject)
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
        Logger.Records.Should().ContainSingle();
        Logger.Records[0].Level.Should().Be(LogLevel.Warning);
        Logger.Records[0].Exception.Should().BeOfType<PostgresException>();
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
        (await CountUsersWithoutIdentityAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ItShouldResolveOneUserWhenCreateIsCalledConcurrently()
    {
        // Arrange
        var subject = NewSubject();
        var email = NewEmail();
        var usersBefore = await CountUsersAsync();
        var identitiesBefore = await CountIdentitiesAsync();

        // Act
        var tasks = Enumerable.Range(0, 8)
            .Select(_ =>
            {
                var (user, identity) = NewUserWithIdentity(email, subject);
                return Sut.CreateAsync(user, identity, CancellationToken.None);
            });
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().OnlyContain(result => result.IsSuccess && result.Value != Guid.Empty);
        results.Select(result => result.Value).Distinct().Should().ContainSingle();
        (await CountIdentitiesAsync(IdentityProviderConstants.Cognito, subject)).Should().Be(1);
        (await CountUsersAsync()).Should().Be(usersBefore + 1);
        (await CountIdentitiesAsync()).Should().Be(identitiesBefore + 1);
        (await CountUsersWithoutIdentityAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ItShouldResolveOneUserWhenResolveIsCalledConcurrently()
    {
        // Arrange
        var subject = NewSubject();
        var email = NewEmail();
        var service = new UserIdentityService(Sut, NullLogger<UserIdentityService>.Instance, TestMapper.Create());
        var usersBefore = await CountUsersAsync();

        // Act
        var tasks = Enumerable.Range(0, 8)
            .Select(_ => service.ResolveAsync(
                new ResolveUserIdentityArgs
                {
                    Provider = IdentityProviderConstants.Cognito,
                    Subject = subject,
                    Email = email
                },
                CancellationToken.None));
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().OnlyContain(result => result.IsSuccess && result.Value != Guid.Empty);
        results.Select(result => result.Value).Distinct().Should().ContainSingle();
        (await CountIdentitiesAsync(IdentityProviderConstants.Cognito, subject)).Should().Be(1);
        (await CountUsersAsync()).Should().Be(usersBefore + 1);
        (await CountUsersWithoutIdentityAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ItShouldCreateAUserAndIdentityWhenTheSubjectIsNew()
    {
        // Arrange
        const string email = "created@example.test";
        var (user, identity) = NewUserWithIdentity(email: email);
        var usersBefore = await CountUsersAsync();
        var identitiesBefore = await CountIdentitiesAsync();

        // Act
        var result = await Sut.CreateAsync(user, identity, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(user.Id);
        result.Value.Should().NotBe(Guid.Empty);
        user.Email.Should().Be(email);
        (await GetEmailAsync(user.Id)).Should().Be(email);
        var found = await Sut.FindUserIdAsync(Lookup(identity.Subject), CancellationToken.None);
        found.IsSuccess.Should().BeTrue();
        found.Value.Should().Be(user.Id);
        (await CountUsersAsync()).Should().Be(usersBefore + 1);
        (await CountIdentitiesAsync()).Should().Be(identitiesBefore + 1);
        (await CountIdentitiesAsync(IdentityProviderConstants.Cognito, identity.Subject)).Should().Be(1);
    }
}
