using AwesomeAssertions;
using FishingLogBook.Web.Offline;

namespace FishingLogBook.Web.Tests.TestCatchPhotoStoreTests;

public class WhenTestingSave
{
    [Fact]
    public async Task ItShouldStillContainPhotograph_WhenNewStoreReadsSamePersistence()
    {
        // Arrange
        var backing = new Dictionary<Guid, TestCatchPhotoBytes>();
        var sut = new MemoryTestCatchPhotoStore(backing);
        var catchId = Guid.Parse("3a1f2c8e-7b44-4d1a-9c3e-2f8a6b0d1e55");
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF };
        await sut.PutAsync(catchId, bytes, "image/jpeg", CancellationToken.None);
        var reopened = new MemoryTestCatchPhotoStore(backing);

        // Act
        var saved = await reopened.GetAsync(catchId, CancellationToken.None);

        // Assert
        saved.Should().NotBeNull();
        saved!.ContentType.Should().Be("image/jpeg");
        saved.Bytes.Should().Equal(bytes);
    }
}
