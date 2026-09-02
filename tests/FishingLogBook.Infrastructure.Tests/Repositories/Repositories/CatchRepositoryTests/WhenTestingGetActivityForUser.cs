using AwesomeAssertions;
using FishingLogBook.Domain.Catches;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;
using FishingLogBook.Shared.Constants;

namespace FishingLogBook.Infrastructure.Tests.Repositories.Repositories.CatchRepositoryTests;

[Collection(PostgresCollection.Name)]
public class WhenTestingGetActivityForUser : BaseCatchRepositoryTest
{
    public WhenTestingGetActivityForUser(PostgresFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task ItShouldReturnAnEmptyListWhenTheUserHasNoCatches()
    {
        // Arrange
        var userId = await CreateUserAsync();

        // Act
        var result = await Sut.GetActivityForUserAsync(userId, CancellationToken.None);

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
        var result = await Sut.GetActivityForUserAsync(ownerId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(catchDetail => catchDetail.Catch.Id == owned.Id);
    }

    [Fact]
    public async Task ItShouldIncludeACatchTheUserRecordedForAnotherAngler()
    {
        // Arrange
        var CaughtByUserId = await CreateUserAsync();
        var recorderUserId = await CreateUserAsync();
        await CreateProfileAsync(CaughtByUserId, "Patrick Connolly");
        var catchId = Guid.NewGuid();
        var recordedForAnother = new Catch
        {
            Id = catchId,
            CaughtByUserId = CaughtByUserId,
            RecordedByUserId = recorderUserId,
            CaughtOn = DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            Photographs =
            [
                new CatchPhotograph
                {
                    Id = Guid.NewGuid(),
                    CatchId = catchId,
                    ContentType = PhotographContentTypeConstants.Jpeg
                }
            ]
        };
        await Sut.UpsertAsync(recordedForAnother, CancellationToken.None);

        // Act
        var anglerActivity = await Sut.GetActivityForUserAsync(CaughtByUserId, CancellationToken.None);
        var recorderActivity = await Sut.GetActivityForUserAsync(recorderUserId, CancellationToken.None);

        // Assert
        anglerActivity.IsSuccess.Should().BeTrue();
        anglerActivity.Value.Should().ContainSingle(catchDetail => catchDetail.Catch.Id == recordedForAnother.Id);
        recorderActivity.IsSuccess.Should().BeTrue();
        var recordedForPatrick = recorderActivity.Value.Should().ContainSingle(
            catchDetail => catchDetail.Catch.Id == recordedForAnother.Id).Subject;
        recordedForPatrick.Catch.CaughtByUserId.Should().Be(CaughtByUserId);
        recordedForPatrick.Catch.CaughtByUserId.Should().Be(CaughtByUserId);
        recordedForPatrick.Catch.RecordedByUserId.Should().Be(recorderUserId);
        recordedForPatrick.AnglerName.Should().Be("Patrick Connolly");
    }

    [Fact]
    public async Task ItShouldProjectDisplayNamesRegardlessOfProfileVisibility()
    {
        // Arrange
        var userId = await CreateUserAsync();
        await CreateProfileAsync(userId, "Hidden Angler", showDisplayName: false);
        var catchRecord = NewCatch(userId);
        await Sut.UpsertAsync(catchRecord, CancellationToken.None);

        // Act
        var result = await Sut.GetActivityForUserAsync(userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var loaded = result.Value.Should().ContainSingle().Subject;
        loaded.AnglerName.Should().Be("Hidden Angler");
        loaded.RecordedByName.Should().Be("Hidden Angler");
    }

    [Fact]
    public async Task ItShouldReturnCatchesNewestFirstWithTheirPhotographs()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var older = NewCatch(userId);
        var olderWithDate = new Catch
        {
            Id = older.Id,
            CaughtByUserId = older.CaughtByUserId,
            RecordedByUserId = older.RecordedByUserId,
            CaughtOn = DateTimeOffset.Parse("2026-08-15T08:00:00Z"),
            Photographs = older.Photographs
        };
        var newer = NewCatch(userId);
        var newerWithDate = new Catch
        {
            Id = newer.Id,
            CaughtByUserId = newer.CaughtByUserId,
            RecordedByUserId = newer.RecordedByUserId,
            CaughtOn = DateTimeOffset.Parse("2026-08-18T08:00:00Z"),
            Photographs = newer.Photographs
        };
        await Sut.UpsertAsync(olderWithDate, CancellationToken.None);
        await Sut.UpsertAsync(newerWithDate, CancellationToken.None);

        // Act
        var result = await Sut.GetActivityForUserAsync(userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value[0].Catch.Id.Should().Be(newerWithDate.Id);
        result.Value[1].Catch.Id.Should().Be(olderWithDate.Id);
        result.Value[0].Catch.Photographs.Should().ContainSingle();
        result.Value[1].Catch.Photographs.Should().ContainSingle();
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
        var result = await Sut.GetActivityForUserAsync(userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value[0].Catch.Photographs.Should().HaveCount(3);
        result.Value[0].Catch.Photographs.Select(photograph => photograph.Id)
            .Should()
            .BeEquivalentTo([firstPhoto, secondPhoto, thirdPhoto]);
        result.Value[0].Catch.Photographs.Should()
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
        var result = await Sut.GetActivityForUserAsync(userId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        var loadedFirst = result.Value.Single(catchDetail => catchDetail.Catch.Id == firstCatchId).Catch;
        var loadedSecond = result.Value.Single(catchDetail => catchDetail.Catch.Id == secondCatchId).Catch;
        loadedFirst.Photographs.Should().HaveCount(2);
        loadedFirst.Photographs.Should().OnlyContain(photograph => photograph.CatchId == firstCatchId);
        loadedSecond.Photographs.Should().HaveCount(2);
        loadedSecond.Photographs.Should().OnlyContain(photograph => photograph.CatchId == secondCatchId);
        loadedFirst.Photographs.Select(photograph => photograph.Id)
            .Should()
            .NotIntersectWith(loadedSecond.Photographs.Select(photograph => photograph.Id));
    }
}
