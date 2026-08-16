using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.TestCatch.Models;
using FishingLogBook.Web.Features.TestCatch.Offline;
using FishingLogBook.Web.Features.TestCatch.Pages.TestCatchLog;
using FishingLogBook.Web.Localization;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.TestCatch.Pages.TestCatchLogTests;

public class WhenTestingPhotograph : BaseTestCatchLogTest
{
    [Fact]
    public async Task ItShouldShowPhotograph_WhenReopened()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var existing = new TestCatchModel(
            Guid.Parse("5a2c8e14-9d70-4b31-8f6a-1c3e7b90d246"),
            "Carp",
            DateTimeOffset.Parse("2026-08-14T17:00:00Z"),
            null,
            SyncStatus.SavedLocally,
            new TestCatchPhotographModel(
                Guid.Parse("aa11bb22-cc33-dd44-ee55-ff6677889900"),
                "image/jpeg",
                SyncStatus.SavedLocally));
        var store = Substitute.For<ITestCatchStore>();
        store.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TestCatchModel>>([existing]));
        var photos = Substitute.For<ITestCatchPhotoStore>();
        photos.GetAsync(existing.Id, Arg.Any<CancellationToken>())
            .Returns(new TestCatchPhotoBytesModel([0xFF, 0xD8, 0xFF], "image/jpeg"));
        await using var context = CreateContext(store, Substitute.For<ITestCatchSynchroniser>(), photos);

        // Act
        var cut = context.Render<TestCatchLog>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find($"#test-catch-photo-{existing.Id}").Should().NotBeNull();
            cut.Find($"#test-catch-photo-{existing.Id}").GetAttribute("src").Should().StartWith("data:image/jpeg;base64,");
            cut.Find($"#test-catch-photo-status-{existing.Id}").TextContent.Should()
                .Contain("Photograph saved locally — not uploaded");
        });
        await store.Received(2).GetAllAsync(Arg.Any<CancellationToken>());
        await photos.Received(2).GetAsync(existing.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowPhotographRetry_WhenUploadFailed()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var existing = new TestCatchModel(
            Guid.Parse("0e7b3c91-5a24-4d86-b1f0-8c2d9e4a6b17"),
            "Tench",
            DateTimeOffset.Parse("2026-08-14T18:00:00Z"),
            null,
            SyncStatus.Synchronised,
            new TestCatchPhotographModel(
                Guid.Parse("bb22cc33-dd44-ee55-ff66-77889900aa11"),
                "image/jpeg",
                SyncStatus.FailedToSynchronise,
                RemoteUrl: null));
        var store = Substitute.For<ITestCatchStore>();
        store.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TestCatchModel>>([existing]));
        var photos = Substitute.For<ITestCatchPhotoStore>();
        photos.GetAsync(existing.Id, Arg.Any<CancellationToken>())
            .Returns(new TestCatchPhotoBytesModel([0xFF, 0xD8, 0xFF], "image/jpeg"));
        var synchroniser = Substitute.For<ITestCatchSynchroniser>();
        await using var context = CreateContext(store, synchroniser, photos);
        var cut = context.Render<TestCatchLog>();

        // Act
        cut.WaitForAssertion(() => cut.Find($"#retry-test-catch-photo-{existing.Id}").Should().NotBeNull());
        await cut.Find($"#retry-test-catch-photo-{existing.Id}").ClickAsync();

        // Assert
        cut.Find($"#test-catch-photo-status-{existing.Id}").TextContent.Should()
            .Contain("Photograph upload failed");
        cut.FindAll($"#retry-test-catch-{existing.Id}").Should().BeEmpty();
        await synchroniser.Received(1).RetryPhotographAsync(existing.Id, Arg.Any<CancellationToken>());
        await photos.Received(3).GetAsync(existing.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowRemotePhotograph_WhenOpenedOnAnotherSession()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var existing = new TestCatchModel(
            Guid.Parse("8c1d4a70-2e59-4b16-9f83-6a0c7d5e1b42"),
            "Rudd",
            DateTimeOffset.Parse("2026-08-14T19:00:00Z"),
            null,
            SyncStatus.Synchronised,
            new TestCatchPhotographModel(
                Guid.Parse("cc33dd44-ee55-ff66-7788-9900aabb1122"),
                "image/jpeg",
                SyncStatus.Synchronised,
                RemoteUrl: "https://storage.test/download/photo"));
        var store = Substitute.For<ITestCatchStore>();
        store.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TestCatchModel>>([existing]));
        var photos = Substitute.For<ITestCatchPhotoStore>();
        photos.GetAsync(existing.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<TestCatchPhotoBytesModel?>(null));
        await using var context = CreateContext(store, Substitute.For<ITestCatchSynchroniser>(), photos);

        // Act
        var cut = context.Render<TestCatchLog>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find($"#test-catch-photo-{existing.Id}").GetAttribute("src")
                .Should().Be("https://storage.test/download/photo");
            cut.Find($"#test-catch-photo-status-{existing.Id}").TextContent.Should()
                .Contain("Photograph uploaded");
        });
        await store.Received(2).GetAllAsync(Arg.Any<CancellationToken>());
        await photos.Received(2).GetAsync(existing.Id, Arg.Any<CancellationToken>());
    }
}
