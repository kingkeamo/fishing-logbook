using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Catches;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;
using FishingLogBook.Shared.Constants;

namespace FishingLogBook.Infrastructure.Tests.Repositories.Repositories.CatchRepositoryTests;

public class WhenTestingDeletePhotograph : BaseCatchRepositoryTest
{
    public WhenTestingDeletePhotograph(PostgresFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task ItShouldNotRemoveThePhotographForAnotherUser()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var otherUserId = await CreateUserAsync();
        var catchRecord = NewCatch(ownerUserId);
        var photograph = catchRecord.Photographs.Single();
        var saved = await Sut.UpsertAsync(catchRecord, CancellationToken.None);
        saved.IsSuccess.Should().BeTrue();

        // Act
        var result = await Sut.DeletePhotographAsync(
            new GetCatchPhotographArgs
            {
                CaughtByUserId = otherUserId,
                CatchId = catchRecord.Id,
                PhotographId = photograph.Id
            },
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var stillOwned = await Sut.GetPhotographAsync(
            new GetCatchPhotographArgs
            {
                CaughtByUserId = ownerUserId,
                CatchId = catchRecord.Id,
                PhotographId = photograph.Id
            },
            CancellationToken.None);
        stillOwned.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task ItShouldBeIdempotentWhenThePhotographWasAlreadyDeleted()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var catchRecord = NewCatch(ownerUserId);
        var photograph = catchRecord.Photographs.Single();
        var saved = await Sut.UpsertAsync(catchRecord, CancellationToken.None);
        saved.IsSuccess.Should().BeTrue();
        var args = new GetCatchPhotographArgs
        {
            CaughtByUserId = ownerUserId,
            CatchId = catchRecord.Id,
            PhotographId = photograph.Id
        };
        var first = await Sut.DeletePhotographAsync(args, CancellationToken.None);
        first.IsSuccess.Should().BeTrue();

        // Act
        var second = await Sut.DeletePhotographAsync(args, CancellationToken.None);

        // Assert
        second.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldOnlyRemoveTheTargetedPhotograph()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var catchId = Guid.NewGuid();
        var toDelete = new CatchPhotograph
        {
            Id = Guid.NewGuid(),
            CatchId = catchId,
            ContentType = PhotographContentTypeConstants.Jpeg
        };
        var toKeep = new CatchPhotograph
        {
            Id = Guid.NewGuid(),
            CatchId = catchId,
            ContentType = PhotographContentTypeConstants.Png
        };
        var catchRecord = NewCatch(ownerUserId, catchId, toDelete, toKeep);
        var saved = await Sut.UpsertAsync(catchRecord, CancellationToken.None);
        saved.IsSuccess.Should().BeTrue();

        // Act
        var result = await Sut.DeletePhotographAsync(
            new GetCatchPhotographArgs
            {
                CaughtByUserId = ownerUserId,
                CatchId = catchId,
                PhotographId = toDelete.Id
            },
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var deleted = await Sut.GetPhotographAsync(
            new GetCatchPhotographArgs { CaughtByUserId = ownerUserId, CatchId = catchId, PhotographId = toDelete.Id },
            CancellationToken.None);
        var kept = await Sut.GetPhotographAsync(
            new GetCatchPhotographArgs { CaughtByUserId = ownerUserId, CatchId = catchId, PhotographId = toKeep.Id },
            CancellationToken.None);
        deleted.Value.Should().BeNull();
        kept.Value.Should().NotBeNull();
    }
}
