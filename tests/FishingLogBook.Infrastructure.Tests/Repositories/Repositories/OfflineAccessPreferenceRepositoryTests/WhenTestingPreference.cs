using AwesomeAssertions;
using FishingLogBook.Infrastructure.Persistence;
using FishingLogBook.Infrastructure.Persistence.Repositories;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;
using FishingLogBook.Tests.Common.Builders;

namespace FishingLogBook.Infrastructure.Tests.Repositories.Repositories.OfflineAccessPreferenceRepositoryTests;

[Collection(PostgresCollection.Name)]
public class WhenTestingPreference
{
    private readonly NpgsqlConnectionFactory _connectionFactory;

    public WhenTestingPreference(PostgresFixture fixture) =>
        _connectionFactory = new NpgsqlConnectionFactory(fixture.ConnectionString);

    [Fact]
    public async Task ItShouldDefaultDisabledAndPreserveTheMostRecentServerEnablementTimestamp()
    {
        var user = new UserBuilder().Build();
        var identity = new UserIdentityBuilder().ForUser(user).Build();
        var identityRepository = new UserIdentityRepository(
            _connectionFactory,
            new RecordingLogger<UserIdentityRepository>());
        await identityRepository.CreateAsync(user, identity, CancellationToken.None);
        var sut = new OfflineAccessPreferenceRepository(
            _connectionFactory,
            new RecordingLogger<OfflineAccessPreferenceRepository>());

        var initial = await sut.GetAsync(user.Id, CancellationToken.None);
        var enabled = await sut.SetAsync(user.Id, true, CancellationToken.None);
        var disabled = await sut.SetAsync(user.Id, false, CancellationToken.None);
        await Task.Delay(10);
        var reenabled = await sut.SetAsync(user.Id, true, CancellationToken.None);

        initial.Value.Enabled.Should().BeFalse();
        initial.Value.EnabledAt.Should().BeNull();
        enabled.Value.Enabled.Should().BeTrue();
        enabled.Value.EnabledAt.Should().NotBeNull();
        disabled.Value.Enabled.Should().BeFalse();
        disabled.Value.EnabledAt.Should().Be(enabled.Value.EnabledAt);
        reenabled.Value.Enabled.Should().BeTrue();
        reenabled.Value.EnabledAt.Should().BeAfter(enabled.Value.EnabledAt!.Value);
    }
}
