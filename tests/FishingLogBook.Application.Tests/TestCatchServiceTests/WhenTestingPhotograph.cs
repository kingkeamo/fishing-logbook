using AwesomeAssertions;
using FishingLogBook.Application.TestCatches;
using FishingLogBook.Shared.Dtos;

namespace FishingLogBook.Application.Tests.TestCatchServiceTests;

public class WhenTestingPhotograph
{
    [Fact]
    public async Task ItShouldKeepASinglePhotograph_WhenTheSameCatchPhotographIsRecordedTwice()
    {
        // Arrange
        var repository = new MemoryTestCatchRepository();
        var sut = new TestCatchService(repository, new MemoryObjectStorage());
        var catchId = Guid.Parse("7a1c3e90-2b44-4d18-9f05-6c8e0a2d1b77");
        var photographId = Guid.Parse("b2d8f014-5c31-4a90-8e26-1f7c4b9a0d55");
        await sut.UpsertAsync(
            new TestCatchDto(catchId, "Carp", DateTimeOffset.Parse("2026-08-14T14:00:00Z"), null),
            CancellationToken.None);
        var request = new RecordPhotographDto(
            photographId,
            $"test-catches/{catchId:D}/{photographId:D}",
            "image/jpeg");

        // Act
        await sut.RecordPhotographAsync(catchId, request, CancellationToken.None);
        await sut.RecordPhotographAsync(catchId, request, CancellationToken.None);
        var listed = await sut.ListAsync(CancellationToken.None);

        // Assert
        listed.Should().ContainSingle();
        listed[0].PhotographId.Should().Be(photographId);
        listed[0].PhotographContentType.Should().Be("image/jpeg");
        listed[0].PhotographUrl.Should().Contain(photographId.ToString("D"));
    }

    [Fact]
    public async Task ItShouldCreateAnUploadUrl_WhenTheCatchExists()
    {
        // Arrange
        var sut = new TestCatchService(new MemoryTestCatchRepository(), new MemoryObjectStorage());
        var catchId = Guid.Parse("0c9e4a12-8d70-4b31-a5c3-7e1f2d8b6a40");
        var photographId = Guid.Parse("5e8b1c24-9a03-4f67-b2d1-0c4e8a7f3519");
        await sut.UpsertAsync(
            new TestCatchDto(catchId, "Tench", DateTimeOffset.Parse("2026-08-14T15:00:00Z"), null),
            CancellationToken.None);

        // Act
        var upload = await sut.CreatePhotographUploadAsync(
            catchId,
            new PhotographUploadRequestDto(photographId, "image/jpeg"),
            CancellationToken.None);

        // Assert
        upload.Should().NotBeNull();
        upload!.ObjectKey.Should().Be($"test-catches/{catchId:D}/{photographId:D}");
        upload.UploadUrl.Should().Contain(upload.ObjectKey);
    }

    [Fact]
    public async Task ItShouldOmitPhotographUrl_WhenObjectStorageIsNotConfigured()
    {
        // Arrange
        var repository = new MemoryTestCatchRepository();
        var configured = new TestCatchService(repository, new MemoryObjectStorage());
        var catchId = Guid.Parse("3f6a9c81-4d20-4e15-b8a7-2c1d0e9f5648");
        var photographId = Guid.Parse("9d2e7b50-1c84-4a36-9f0d-8b5c3a1e7260");
        await configured.UpsertAsync(
            new TestCatchDto(catchId, "Roach", DateTimeOffset.Parse("2026-08-14T16:00:00Z"), null),
            CancellationToken.None);
        await configured.RecordPhotographAsync(
            catchId,
            new RecordPhotographDto(photographId, $"test-catches/{catchId:D}/{photographId:D}", "image/jpeg"),
            CancellationToken.None);
        var sut = new TestCatchService(repository, new MemoryObjectStorage { IsConfigured = false });

        // Act
        var listed = await sut.ListAsync(CancellationToken.None);

        // Assert
        listed.Should().ContainSingle();
        listed[0].PhotographId.Should().Be(photographId);
        listed[0].PhotographUrl.Should().BeNull();
    }
}
