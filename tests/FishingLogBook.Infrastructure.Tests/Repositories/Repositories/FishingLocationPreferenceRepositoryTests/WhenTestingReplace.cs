using AwesomeAssertions;
using FishingLogBook.Application.FishingLocations.Errors;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;
using FishingLogBook.Shared.Constants;
using Npgsql;

namespace FishingLogBook.Infrastructure.Tests.Repositories.Repositories.FishingLocationPreferenceRepositoryTests;

[Collection(PostgresCollection.Name)]
public class WhenTestingReplace : BaseFishingLocationPreferenceRepositoryTest
{
    public WhenTestingReplace(PostgresFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task ItShouldFailWhenTheUserDoesNotExist()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var result = await Sut.ReplaceAsync(
            userId,
            [Location(userId, "Lough Corrib", true)],
            CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Message.Should().Be("Failed to save fishing locations.");
        LoggedSqlStates().Should().Contain(PostgresErrorCodes.ForeignKeyViolation);
    }

    [Fact]
    public async Task ItShouldRejectABlankName()
    {
        // Arrange
        var userId = await CreateUserAsync();

        // Act
        var result = await Sut.ReplaceAsync(
            userId,
            [Location(userId, "   ")],
            CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        LoggedSqlStates().Should().Contain(PostgresErrorCodes.CheckViolation);
        var stored = await Sut.GetByUserIdAsync(userId, CancellationToken.None);
        stored.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldRejectANameLongerThanTheMaximum()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var tooLong = new string('a', FishingLocationConstants.MaxNameLength + 1);

        // Act
        var result = await Sut.ReplaceAsync(
            userId,
            [Location(userId, tooLong)],
            CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        LoggedSqlStates().Should().Contain(PostgresErrorCodes.CheckViolation);
        var stored = await Sut.GetByUserIdAsync(userId, CancellationToken.None);
        stored.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldAcceptANameOfExactlyTheMaximumLength()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var atLimit = new string('a', FishingLocationConstants.MaxNameLength);

        // Act
        var result = await Sut.ReplaceAsync(
            userId,
            [Location(userId, atLimit)],
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var stored = await Sut.GetByUserIdAsync(userId, CancellationToken.None);
        stored.Value.Single().Name.Should().Be(atLimit);
    }

    [Theory]
    [InlineData("Lough Corrib", "lough corrib")]
    [InlineData("Lough Corrib", " Lough Corrib ")]
    [InlineData("Lough Corrib", "LOUGH CORRIB")]
    public async Task ItShouldRejectASensibleDuplicateName(string first, string second)
    {
        // Arrange
        var userId = await CreateUserAsync();

        // Act
        var result = await Sut.ReplaceAsync(
            userId,
            [Location(userId, first), Location(userId, second)],
            CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<DuplicateFishingLocationError>();
        LoggedSqlStates().Should().Contain(PostgresErrorCodes.UniqueViolation);
        var stored = await Sut.GetByUserIdAsync(userId, CancellationToken.None);
        stored.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldRejectTwoDefaultLocations()
    {
        // Arrange
        var userId = await CreateUserAsync();

        // Act
        var result = await Sut.ReplaceAsync(
            userId,
            [Location(userId, "Lough Corrib", true), Location(userId, "River Moy", true)],
            CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors[0].Should().BeOfType<DuplicateFishingLocationError>();
        LoggedSqlStates().Should().Contain(PostgresErrorCodes.UniqueViolation);
        var stored = await Sut.GetByUserIdAsync(userId, CancellationToken.None);
        stored.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldAllowTheSameNameForTwoDifferentAnglers()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var otherUserId = await CreateUserAsync();

        // Act
        var owner = await Sut.ReplaceAsync(
            ownerUserId,
            [Location(ownerUserId, "Lough Corrib", true)],
            CancellationToken.None);
        var other = await Sut.ReplaceAsync(
            otherUserId,
            [Location(otherUserId, "Lough Corrib", true)],
            CancellationToken.None);

        // Assert
        owner.IsSuccess.Should().BeTrue();
        other.IsSuccess.Should().BeTrue();
        var stored = await Sut.GetByUserIdAsync(otherUserId, CancellationToken.None);
        stored.Value.Single().Name.Should().Be("Lough Corrib");
    }

    [Fact]
    public async Task ItShouldSaveNoLocationsWhenTheAnglerClearsTheList()
    {
        // Arrange
        var userId = await CreateUserAsync();
        await Sut.ReplaceAsync(
            userId,
            [Location(userId, "Lough Corrib", true)],
            CancellationToken.None);

        // Act
        var result = await Sut.ReplaceAsync(userId, [], CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var stored = await Sut.GetByUserIdAsync(userId, CancellationToken.None);
        stored.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldRemoveOnlyTheDeletedLocation()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var corrib = Location(userId, "Lough Corrib", true);
        var moy = Location(userId, "River Moy");
        await Sut.ReplaceAsync(userId, [corrib, moy], CancellationToken.None);

        // Act
        var result = await Sut.ReplaceAsync(userId, [corrib], CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var stored = await Sut.GetByUserIdAsync(userId, CancellationToken.None);
        stored.Value.Select(location => location.Name).Should().Equal("Lough Corrib");
        stored.Value.Single().Id.Should().Be(corrib.Id);
    }

    [Fact]
    public async Task ItShouldLeaveNoDefaultWhenTheDefaultLocationIsRemoved()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var corrib = Location(userId, "Lough Corrib", true);
        var moy = Location(userId, "River Moy");
        await Sut.ReplaceAsync(userId, [corrib, moy], CancellationToken.None);

        // Act
        var result = await Sut.ReplaceAsync(userId, [moy], CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var stored = await Sut.GetByUserIdAsync(userId, CancellationToken.None);
        stored.Value.Select(location => location.Name).Should().Equal("River Moy");
        stored.Value.Should().OnlyContain(location => !location.IsDefault);
    }

    [Fact]
    public async Task ItShouldMoveTheDefaultToAnotherLocation()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var corrib = Location(userId, "Lough Corrib", true);
        var moy = Location(userId, "River Moy");
        await Sut.ReplaceAsync(userId, [corrib, moy], CancellationToken.None);

        // Act
        var result = await Sut.ReplaceAsync(
            userId,
            [Location(userId, "Lough Corrib", false, corrib.Id), Location(userId, "River Moy", true, moy.Id)],
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var stored = await Sut.GetByUserIdAsync(userId, CancellationToken.None);
        stored.Value.Single(location => location.IsDefault).Name.Should().Be("River Moy");
        stored.Value.Count(location => location.IsDefault).Should().Be(1);
    }

    [Fact]
    public async Task ItShouldSaveSeveralLocationsWithOneDefault()
    {
        // Arrange
        var userId = await CreateUserAsync();

        // Act
        var result = await Sut.ReplaceAsync(
            userId,
            [
                Location(userId, "Lough Corrib", true),
                Location(userId, "Lough Mask"),
                Location(userId, "River Moy")
            ],
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var stored = await Sut.GetByUserIdAsync(userId, CancellationToken.None);
        stored.Value.Should().HaveCount(3);
        stored.Value.Single(location => location.IsDefault).Name.Should().Be("Lough Corrib");
    }
}
