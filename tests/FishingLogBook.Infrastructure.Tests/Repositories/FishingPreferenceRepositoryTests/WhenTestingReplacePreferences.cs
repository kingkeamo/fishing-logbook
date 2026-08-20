using AwesomeAssertions;
using FishingLogBook.Domain.Catalogue;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;
using Npgsql;

namespace FishingLogBook.Infrastructure.Tests.Repositories.FishingPreferenceRepositoryTests;

[Collection(PostgresCollection.Name)]
public class WhenTestingReplacePreferences : BaseFishingPreferenceRepositoryTest
{
    public WhenTestingReplacePreferences(PostgresFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task ItShouldFailWhenTheUserDoesNotExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var flyId = await MethodIdAsync("Fly");

        // Act
        var result = await Sut.ReplacePreferencesAsync(
            userId,
            [MethodPreference(userId, flyId, true)],
            [],
            CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("Failed to save fishing preferences.");
        LoggedSqlStates().Should().Contain(PostgresErrorCodes.ForeignKeyViolation);
    }

    [Fact]
    public async Task ItShouldFailWhenTheFishingMethodIsNotInTheCatalogue()
    {
        // Arrange
        var userId = await CreateUserAsync();

        // Act
        var result = await Sut.ReplacePreferencesAsync(
            userId,
            [MethodPreference(userId, Guid.NewGuid(), true)],
            [],
            CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        var stored = await Sut.GetMethodPreferencesAsync(userId, CancellationToken.None);
        stored.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldFailWhenTwoMethodsAreMarkedAsDefault()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var flyId = await MethodIdAsync("Fly");
        var spinningId = await MethodIdAsync("Spinning");

        // Act
        var result = await Sut.ReplacePreferencesAsync(
            userId,
            [MethodPreference(userId, flyId, true), MethodPreference(userId, spinningId, true)],
            [],
            CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        LoggedSqlStates().Should().Contain(PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task ItShouldFailWhenTwoSpeciesAreMarkedAsDefaultForOneMethod()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var flyId = await MethodIdAsync("Fly");
        var brownTroutId = await SpeciesIdAsync("BrownTrout");
        var pikeId = await SpeciesIdAsync("Pike");

        // Act
        var result = await Sut.ReplacePreferencesAsync(
            userId,
            [MethodPreference(userId, flyId, true)],
            [
                SpeciesPreference(userId, flyId, brownTroutId, true),
                SpeciesPreference(userId, flyId, pikeId, true)
            ],
            CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        LoggedSqlStates().Should().Contain(PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task ItShouldRollBackEveryChangeWhenTheSpeciesInsertFails()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var flyId = await MethodIdAsync("Fly");
        var spinningId = await MethodIdAsync("Spinning");
        var brownTroutId = await SpeciesIdAsync("BrownTrout");
        await Sut.ReplacePreferencesAsync(
            userId,
            [MethodPreference(userId, flyId, true)],
            [SpeciesPreference(userId, flyId, brownTroutId, true)],
            CancellationToken.None);

        // Act
        var result = await Sut.ReplacePreferencesAsync(
            userId,
            [MethodPreference(userId, spinningId, true)],
            [SpeciesPreference(userId, spinningId, Guid.NewGuid(), true)],
            CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        var storedMethods = await Sut.GetMethodPreferencesAsync(userId, CancellationToken.None);
        var storedSpecies = await Sut.GetSpeciesPreferencesAsync(userId, CancellationToken.None);
        storedMethods.Value.Should().ContainSingle(method => method.FishingMethodId == flyId);
        storedSpecies.Value.Should().ContainSingle(species => species.SpeciesId == brownTroutId);
    }

    [Fact]
    public async Task ItShouldRemoveEveryPreferenceWhenTheReplacementIsEmpty()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var flyId = await MethodIdAsync("Fly");
        var brownTroutId = await SpeciesIdAsync("BrownTrout");
        await Sut.ReplacePreferencesAsync(
            userId,
            [MethodPreference(userId, flyId, true)],
            [SpeciesPreference(userId, flyId, brownTroutId, true)],
            CancellationToken.None);

        // Act
        var result = await Sut.ReplacePreferencesAsync(userId, [], [], CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var storedMethods = await Sut.GetMethodPreferencesAsync(userId, CancellationToken.None);
        var storedSpecies = await Sut.GetSpeciesPreferencesAsync(userId, CancellationToken.None);
        storedMethods.Value.Should().BeEmpty();
        storedSpecies.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldReplaceThePreviousSelectionWithoutTouchingAnotherUser()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var otherUserId = await CreateUserAsync();
        var flyId = await MethodIdAsync("Fly");
        var spinningId = await MethodIdAsync("Spinning");
        var brownTroutId = await SpeciesIdAsync("BrownTrout");
        var pikeId = await SpeciesIdAsync("Pike");
        await Sut.ReplacePreferencesAsync(
            otherUserId,
            [MethodPreference(otherUserId, flyId, true)],
            [SpeciesPreference(otherUserId, flyId, brownTroutId, true)],
            CancellationToken.None);
        await Sut.ReplacePreferencesAsync(
            userId,
            [MethodPreference(userId, flyId, true)],
            [SpeciesPreference(userId, flyId, brownTroutId, true)],
            CancellationToken.None);

        // Act
        var result = await Sut.ReplacePreferencesAsync(
            userId,
            [MethodPreference(userId, spinningId, true)],
            [SpeciesPreference(userId, spinningId, pikeId, true)],
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var storedMethods = await Sut.GetMethodPreferencesAsync(userId, CancellationToken.None);
        var storedSpecies = await Sut.GetSpeciesPreferencesAsync(userId, CancellationToken.None);
        storedMethods.Value.Should().ContainSingle(method =>
            method.UserId == userId && method.FishingMethodId == spinningId && method.IsDefault);
        storedSpecies.Value.Should().ContainSingle(species =>
            species.UserId == userId
            && species.FishingMethodId == spinningId
            && species.SpeciesId == pikeId
            && species.IsDefault);
        var otherMethods = await Sut.GetMethodPreferencesAsync(otherUserId, CancellationToken.None);
        otherMethods.Value.Should().ContainSingle(method => method.FishingMethodId == flyId);
    }

    [Fact]
    public async Task ItShouldPersistSeveralMethodsWithTheirSpecies()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var flyId = await MethodIdAsync("Fly");
        var spinningId = await MethodIdAsync("Spinning");
        var brownTroutId = await SpeciesIdAsync("BrownTrout");
        var pikeId = await SpeciesIdAsync("Pike");
        IReadOnlyList<UserFishingMethodPreference> methods =
        [
            MethodPreference(userId, flyId, true),
            MethodPreference(userId, spinningId)
        ];
        IReadOnlyList<UserFishingSpeciesPreference> species =
        [
            SpeciesPreference(userId, flyId, brownTroutId, true),
            SpeciesPreference(userId, flyId, pikeId),
            SpeciesPreference(userId, spinningId, pikeId, true)
        ];

        // Act
        var result = await Sut.ReplacePreferencesAsync(userId, methods, species, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var storedMethods = await Sut.GetMethodPreferencesAsync(userId, CancellationToken.None);
        var storedSpecies = await Sut.GetSpeciesPreferencesAsync(userId, CancellationToken.None);
        storedMethods.Value.Should().HaveCount(2);
        storedMethods.Value.Should().ContainSingle(method => method.IsDefault && method.FishingMethodId == flyId);
        storedSpecies.Value.Should().HaveCount(3);
        storedSpecies.Value.Should().ContainSingle(item =>
            item.FishingMethodId == flyId && item.SpeciesId == brownTroutId && item.IsDefault);
        storedSpecies.Value.Should().ContainSingle(item =>
            item.FishingMethodId == spinningId && item.SpeciesId == pikeId && item.IsDefault);
        storedSpecies.Value.Should().OnlyContain(item => item.CreatedOn != default);
    }

    private IReadOnlyList<string?> LoggedSqlStates()
    {
        return
        [
            .. Logger.Records
                .Select(record => record.Exception)
                .OfType<PostgresException>()
                .Select(exception => exception.SqlState)
        ];
    }
}
