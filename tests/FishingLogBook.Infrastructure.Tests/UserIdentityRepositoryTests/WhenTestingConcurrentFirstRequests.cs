using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Users.Services;
using FishingLogBook.Infrastructure.Tests.TestSupport;
using FishingLogBook.Shared.Constants;
using Microsoft.Extensions.Logging.Abstractions;

namespace FishingLogBook.Infrastructure.Tests.UserIdentityRepositoryTests;

[Collection(PostgresCollection.Name)]
public class WhenTestingConcurrentFirstRequests : BaseUserIdentityRepositoryTest
{
    public WhenTestingConcurrentFirstRequests(PostgresFixture fixture)
        : base(fixture)
    {
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
        var service = new UserIdentityService(Sut, NullLogger<UserIdentityService>.Instance);
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
}
