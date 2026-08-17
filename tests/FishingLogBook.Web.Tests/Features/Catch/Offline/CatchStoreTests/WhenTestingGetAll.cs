using AwesomeAssertions;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Features.Catch.Models;

namespace FishingLogBook.Web.Tests.Features.Catch.Offline.CatchStoreTests;

public class WhenTestingGetAll : BaseCatchStoreTest
{
    [Fact]
    public async Task ItShouldReturnAnEmptyListWhenNothingIsSaved()
    {
        // Arrange
        // Act
        var saved = await Sut.GetAllAsync(CancellationToken.None);

        // Assert
        saved.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldReturnSavedCatchesWithPhotographBytes()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        var photographId = Guid.NewGuid();
        await Sut.SaveAsync(
            new CatchModel(
                catchId,
                DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
                [new CatchPhotographModel(photographId, catchId, PhotographContentTypeConstants.Jpeg, [4, 5, 6])]),
            CancellationToken.None);

        // Act
        var saved = await Sut.GetAllAsync(CancellationToken.None);

        // Assert
        saved.Should().ContainSingle();
        saved[0].Id.Should().Be(catchId);
        saved[0].Photographs[0].Id.Should().Be(photographId);
        saved[0].Photographs[0].Bytes.Should().Equal(4, 5, 6);
    }
}
