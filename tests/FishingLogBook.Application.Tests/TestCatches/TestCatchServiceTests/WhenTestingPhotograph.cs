using AwesomeAssertions;
using FishingLogBook.Domain.TestCatches;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Application.Tests.TestCatches.TestCatchServiceTests;

public class WhenTestingPhotograph : BaseTestCatchServiceTest
{
    [Fact]
    public async Task ItShouldKeepASinglePhotograph_WhenTheSameCatchPhotographIsRecordedTwice()
    {
        // Arrange
        var catchId = Guid.Parse("7a1c3e90-2b44-4d18-9f05-6c8e0a2d1b77");
        var photographId = Guid.Parse("b2d8f014-5c31-4a90-8e26-1f7c4b9a0d55");
        var objectKey = $"test-catches/{catchId:D}/{photographId:D}";
        MockTestCatchRepository
            .GetByIdAsync(catchId, Arg.Any<CancellationToken>())
            .Returns(new TestCatchRecord
            {
                Id = catchId,
                SpeciesName = "Carp",
                CaughtOn = DateTimeOffset.Parse("2026-08-14T14:00:00Z")
            });
        var request = new RecordPhotographDto(photographId, objectKey, "image/jpeg");

        // Act
        var first = await Sut.RecordPhotographAsync(catchId, request, CancellationToken.None);
        var second = await Sut.RecordPhotographAsync(catchId, request, CancellationToken.None);

        // Assert
        first.Should().BeTrue();
        second.Should().BeTrue();
        await MockTestCatchRepository.Received(2).GetByIdAsync(catchId, Arg.Any<CancellationToken>());
        await MockTestCatchRepository.Received(2).UpsertPhotographAsync(
            catchId,
            photographId,
            objectKey,
            "image/jpeg",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldCreateAnUploadUrl_WhenTheCatchExists()
    {
        // Arrange
        var catchId = Guid.Parse("0c9e4a12-8d70-4b31-a5c3-7e1f2d8b6a40");
        var photographId = Guid.Parse("5e8b1c24-9a03-4f67-b2d1-0c4e8a7f3519");
        var objectKey = $"test-catches/{catchId:D}/{photographId:D}";
        MockTestCatchRepository
            .GetByIdAsync(catchId, Arg.Any<CancellationToken>())
            .Returns(new TestCatchRecord
            {
                Id = catchId,
                SpeciesName = "Tench",
                CaughtOn = DateTimeOffset.Parse("2026-08-14T15:00:00Z")
            });
        MockObjectStorage
            .CreateUploadUrlAsync(
                objectKey,
                "image/jpeg",
                Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
            .Returns(new Uri($"https://storage.test/upload/{objectKey}"));

        // Act
        var upload = await Sut.CreatePhotographUploadAsync(
            catchId,
            new PhotographUploadRequestDto(photographId, "image/jpeg"),
            CancellationToken.None);

        // Assert
        upload.Should().NotBeNull();
        upload!.ObjectKey.Should().Be(objectKey);
        upload.UploadUrl.Should().Contain(objectKey);
        await MockTestCatchRepository.Received(1).GetByIdAsync(catchId, Arg.Any<CancellationToken>());
        await MockObjectStorage.Received(1).CreateUploadUrlAsync(
            objectKey,
            "image/jpeg",
            TimeSpan.FromMinutes(15),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldOmitPhotographUrl_WhenObjectStorageIsNotConfigured()
    {
        // Arrange
        var catchId = Guid.Parse("3f6a9c81-4d20-4e15-b8a7-2c1d0e9f5648");
        var photographId = Guid.Parse("9d2e7b50-1c84-4a36-9f0d-8b5c3a1e7260");
        var objectKey = $"test-catches/{catchId:D}/{photographId:D}";
        MockObjectStorage.IsConfigured.Returns(false);
        MockTestCatchRepository
            .GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TestCatchRecord>>(
            [
                new TestCatchRecord
                {
                    Id = catchId,
                    SpeciesName = "Roach",
                    CaughtOn = DateTimeOffset.Parse("2026-08-14T16:00:00Z"),
                    PhotographId = photographId,
                    PhotographObjectKey = objectKey,
                    PhotographContentType = "image/jpeg"
                }
            ]));

        // Act
        var listed = await Sut.ListAsync(CancellationToken.None);

        // Assert
        listed.Should().ContainSingle();
        listed[0].PhotographId.Should().Be(photographId);
        listed[0].PhotographUrl.Should().BeNull();
        await MockTestCatchRepository.Received(1).GetAllAsync(Arg.Any<CancellationToken>());
        await MockObjectStorage.DidNotReceive().CreateDownloadUrlAsync(
            Arg.Any<string>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }
}
