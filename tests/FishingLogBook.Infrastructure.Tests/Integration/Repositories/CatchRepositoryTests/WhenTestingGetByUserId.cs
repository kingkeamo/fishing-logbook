using AwesomeAssertions;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;

namespace FishingLogBook.Infrastructure.Tests.Repositories.CatchRepositoryTests;

[Collection(PostgresCollection.Name)]
public class WhenTestingGetByUserId : BaseCatchRepositoryTest
{
    public WhenTestingGetByUserId(PostgresFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task ItShouldReturnAnEmptyListWhenTheUserHasNoCatches()
    {
        // Arrange
        var userId = await CreateUserAsync();

        // Act
        var result = await Sut.GetByUserIdAsync(userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldNotReturnAnotherUsersCatches()
    {
        // Arrange
        var ownerId = await CreateUserAsync();
        var otherUserId = await CreateUserAsync();
        var owned = NewCatch(ownerId);
        var other = NewCatch(otherUserId);
        await Sut.UpsertAsync(owned, CancellationToken.None);
        await Sut.UpsertAsync(other, CancellationToken.None);

        // Act
        var result = await Sut.GetByUserIdAsync(ownerId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(catchRecord => catchRecord.Id == owned.Id);
    }

    [Fact]
    public async Task ItShouldReturnCatchesNewestFirstWithTheirPhotographs()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var older = NewCatch(userId);
        var olderWithDate = new Domain.Catches.Catch
        {
            Id = older.Id,
            UserId = older.UserId,
            AnglerUserId = older.AnglerUserId,
            RecordedByUserId = older.RecordedByUserId,
            CaughtOn = DateTimeOffset.Parse("2026-08-15T08:00:00Z"),
            Photographs = older.Photographs
        };
        var newer = NewCatch(userId);
        var newerWithDate = new Domain.Catches.Catch
        {
            Id = newer.Id,
            UserId = newer.UserId,
            AnglerUserId = newer.AnglerUserId,
            RecordedByUserId = newer.RecordedByUserId,
            CaughtOn = DateTimeOffset.Parse("2026-08-18T08:00:00Z"),
            Photographs = newer.Photographs
        };
        await Sut.UpsertAsync(olderWithDate, CancellationToken.None);
        await Sut.UpsertAsync(newerWithDate, CancellationToken.None);

        // Act
        var result = await Sut.GetByUserIdAsync(userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].Id.Should().Be(newerWithDate.Id);
        result.Value[1].Id.Should().Be(olderWithDate.Id);
        result.Value[0].Photographs.Should().ContainSingle();
        result.Value[1].Photographs.Should().ContainSingle();
    }
}
