using AwesomeAssertions;
using FishingLogBook.Application.Args;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;

namespace FishingLogBook.Infrastructure.Tests.Repositories.CatchRepositoryTests;

public class WhenTestingGetPhotograph : BaseCatchRepositoryTest
{
    public WhenTestingGetPhotograph(PostgresFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task ItShouldReturnThePhotographForItsOwner()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var catchRecord = NewCatch(ownerUserId);
        var photograph = catchRecord.Photographs.Single();
        var saved = await Sut.UpsertAsync(catchRecord, CancellationToken.None);
        saved.IsSuccess.Should().BeTrue();

        // Act
        var result = await Sut.GetPhotographAsync(
            new GetCatchPhotographArgs
            {
                UserId = ownerUserId,
                CatchId = catchRecord.Id,
                PhotographId = photograph.Id
            },
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be(photograph.Id);
        result.Value.CatchId.Should().Be(catchRecord.Id);
        result.Value.ContentType.Should().Be(photograph.ContentType);
    }

    [Fact]
    public async Task ItShouldNotReturnThePhotographForAnotherUser()
    {
        // Arrange
        var ownerUserId = await CreateUserAsync();
        var otherUserId = await CreateUserAsync();
        var catchRecord = NewCatch(ownerUserId);
        var photograph = catchRecord.Photographs.Single();
        var saved = await Sut.UpsertAsync(catchRecord, CancellationToken.None);
        saved.IsSuccess.Should().BeTrue();

        // Act
        var result = await Sut.GetPhotographAsync(
            new GetCatchPhotographArgs
            {
                UserId = otherUserId,
                CatchId = catchRecord.Id,
                PhotographId = photograph.Id
            },
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }
}
