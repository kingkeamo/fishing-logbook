using AwesomeAssertions;
using FishingLogBook.Domain.Catches;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;
using FishingLogBook.Shared.Constants;

namespace FishingLogBook.Infrastructure.Tests.Repositories.Repositories.CatchRepositoryTests;

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

    [Fact]
    public async Task ItShouldGroupMultiplePhotographsUnderTheirOwningCatch()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var catchId = Guid.NewGuid();
        var firstPhoto = Guid.NewGuid();
        var secondPhoto = Guid.NewGuid();
        var thirdPhoto = Guid.NewGuid();
        var catchRecord = NewCatch(
            userId,
            catchId,
            new CatchPhotograph
            {
                Id = firstPhoto,
                CatchId = catchId,
                ContentType = PhotographContentTypeConstants.Jpeg
            },
            new CatchPhotograph
            {
                Id = secondPhoto,
                CatchId = catchId,
                ContentType = PhotographContentTypeConstants.Png
            },
            new CatchPhotograph
            {
                Id = thirdPhoto,
                CatchId = catchId,
                ContentType = PhotographContentTypeConstants.Webp
            });
        await Sut.UpsertAsync(catchRecord, CancellationToken.None);

        // Act
        var result = await Sut.GetByUserIdAsync(userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value[0].Photographs.Should().HaveCount(3);
        result.Value[0].Photographs.Select(photograph => photograph.Id)
            .Should()
            .BeEquivalentTo([firstPhoto, secondPhoto, thirdPhoto]);
        result.Value[0].Photographs.Should()
            .OnlyContain(photograph => photograph.CatchId == catchId);
    }

    [Fact]
    public async Task ItShouldNotMixPhotographsBetweenMultiplePhotographCatches()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var firstCatchId = Guid.NewGuid();
        var secondCatchId = Guid.NewGuid();
        var firstCatch = NewCatch(
            userId,
            firstCatchId,
            new CatchPhotograph
            {
                Id = Guid.NewGuid(),
                CatchId = firstCatchId,
                ContentType = PhotographContentTypeConstants.Jpeg
            },
            new CatchPhotograph
            {
                Id = Guid.NewGuid(),
                CatchId = firstCatchId,
                ContentType = PhotographContentTypeConstants.Png
            });
        var secondCatch = NewCatch(
            userId,
            secondCatchId,
            new CatchPhotograph
            {
                Id = Guid.NewGuid(),
                CatchId = secondCatchId,
                ContentType = PhotographContentTypeConstants.Webp
            },
            new CatchPhotograph
            {
                Id = Guid.NewGuid(),
                CatchId = secondCatchId,
                ContentType = PhotographContentTypeConstants.Jpeg
            });
        await Sut.UpsertAsync(firstCatch, CancellationToken.None);
        await Sut.UpsertAsync(secondCatch, CancellationToken.None);

        // Act
        var result = await Sut.GetByUserIdAsync(userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        var loadedFirst = result.Value.Single(catchRecord => catchRecord.Id == firstCatchId);
        var loadedSecond = result.Value.Single(catchRecord => catchRecord.Id == secondCatchId);
        loadedFirst.Photographs.Should().HaveCount(2);
        loadedFirst.Photographs.Should().OnlyContain(photograph => photograph.CatchId == firstCatchId);
        loadedSecond.Photographs.Should().HaveCount(2);
        loadedSecond.Photographs.Should().OnlyContain(photograph => photograph.CatchId == secondCatchId);
        loadedFirst.Photographs.Select(photograph => photograph.Id)
            .Should()
            .NotIntersectWith(loadedSecond.Photographs.Select(photograph => photograph.Id));
    }
}
