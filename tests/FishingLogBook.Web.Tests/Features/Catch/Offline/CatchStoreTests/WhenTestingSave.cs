using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Features.Catch.Models;

namespace FishingLogBook.Web.Tests.Features.Catch.Offline.CatchStoreTests;

public class WhenTestingSave : BaseCatchStoreTest
{
    [Fact]
    public async Task ItShouldRejectACatchWithoutPhotographs()
    {
        // Arrange
        var catchRecord = new CatchModel(Guid.NewGuid(), DateTimeOffset.UtcNow, []);

        // Act
        var act = () => Sut.SaveAsync(catchRecord, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        (await Sut.GetAllAsync(CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldNotLeaveACatchWhenPhotographPersistenceFails()
    {
        // Arrange
        Sut.FailPhotographWrite = true;
        var catchId = Guid.NewGuid();
        var photographId = Guid.NewGuid();
        var catchRecord = new CatchModel(
            catchId,
            DateTimeOffset.UtcNow,
            [new CatchPhotographModel(photographId, catchId, PhotographContentTypeConstants.Jpeg, [1, 2, 3])]);

        // Act
        var act = () => Sut.SaveAsync(catchRecord, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        (await Sut.GetAllAsync(CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldKeepCatchAndPhotographIdsAfterReopen()
    {
        // Arrange
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var photographId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var catchRecord = new CatchModel(
            catchId,
            DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            [new CatchPhotographModel(photographId, catchId, PhotographContentTypeConstants.Png, [9, 8, 7])]);
        await Sut.SaveAsync(catchRecord, CancellationToken.None);
        var reopened = new MemoryCatchStore(BackingCatches, BackingPhotographs);

        // Act
        var saved = await reopened.GetAllAsync(CancellationToken.None);

        // Assert
        saved.Should().ContainSingle();
        saved[0].Id.Should().Be(catchId);
        saved[0].Photographs.Should().ContainSingle();
        saved[0].Photographs[0].Id.Should().Be(photographId);
        saved[0].Photographs[0].Bytes.Should().Equal(9, 8, 7);
        saved[0].SpeciesName.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldAssignDistinctIdsToSeparatelySavedCatches()
    {
        // Arrange
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var firstPhoto = Guid.NewGuid();
        var secondPhoto = Guid.NewGuid();
        await Sut.SaveAsync(
            new CatchModel(
                firstId,
                DateTimeOffset.UtcNow,
                [new CatchPhotographModel(firstPhoto, firstId, PhotographContentTypeConstants.Jpeg, [1])]),
            CancellationToken.None);

        // Act
        await Sut.SaveAsync(
            new CatchModel(
                secondId,
                DateTimeOffset.UtcNow,
                [new CatchPhotographModel(secondPhoto, secondId, PhotographContentTypeConstants.Jpeg, [2])]),
            CancellationToken.None);
        var saved = await Sut.GetAllAsync(CancellationToken.None);

        // Assert
        saved.Should().HaveCount(2);
        saved.Select(item => item.Id).Should().BeEquivalentTo([firstId, secondId]);
        saved.SelectMany(item => item.Photographs).Select(photograph => photograph.Id)
            .Should()
            .BeEquivalentTo([firstPhoto, secondPhoto]);
        firstId.Should().NotBe(secondId);
        firstPhoto.Should().NotBe(secondPhoto);
    }
}
