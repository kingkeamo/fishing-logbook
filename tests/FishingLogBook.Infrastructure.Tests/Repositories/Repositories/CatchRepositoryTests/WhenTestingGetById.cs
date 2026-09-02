using AwesomeAssertions;
using FishingLogBook.Domain.Catches;
using FishingLogBook.Infrastructure.Tests.Repositories.TestSupport;
using FishingLogBook.Shared.Constants;

namespace FishingLogBook.Infrastructure.Tests.Repositories.Repositories.CatchRepositoryTests;

[Collection(PostgresCollection.Name)]
public class WhenTestingGetById : BaseCatchRepositoryTest
{
    public WhenTestingGetById(PostgresFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task ItShouldReturnNullWhenTheCatchDoesNotExist()
    {
        // Arrange
        var missingId = Guid.NewGuid();

        // Act
        var result = await Sut.GetByIdAsync(missingId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldLoadTheCatchAndPhotographIds()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var catchRecord = NewCatch(userId);
        await Sut.UpsertAsync(catchRecord, CancellationToken.None);

        // Act
        var result = await Sut.GetByIdAsync(catchRecord.Id, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be(catchRecord.Id);
        result.Value.CaughtByUserId.Should().Be(userId);
        result.Value.CaughtByUserId.Should().Be(userId);
        result.Value.RecordedByUserId.Should().Be(userId);
        result.Value.Photographs.Should().ContainSingle();
        result.Value.Photographs[0].Id.Should().Be(catchRecord.Photographs[0].Id);
        result.Value.Photographs[0].CatchId.Should().Be(catchRecord.Id);
        result.Value.Location.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldLoadEveryPhotographForACatchWithMultiplePhotographs()
    {
        // Arrange
        var userId = await CreateUserAsync();
        var catchId = Guid.NewGuid();
        var firstPhoto = Guid.NewGuid();
        var secondPhoto = Guid.NewGuid();
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
            });
        await Sut.UpsertAsync(catchRecord, CancellationToken.None);

        // Act
        var result = await Sut.GetByIdAsync(catchId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Photographs.Should().HaveCount(2);
        result.Value.Photographs.Select(photograph => photograph.Id)
            .Should()
            .BeEquivalentTo([firstPhoto, secondPhoto]);
        result.Value.Photographs.Should().OnlyContain(photograph => photograph.CatchId == catchId);
    }
}
