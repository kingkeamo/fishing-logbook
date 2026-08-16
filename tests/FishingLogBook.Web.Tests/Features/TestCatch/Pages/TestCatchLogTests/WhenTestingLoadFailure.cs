using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.TestCatch.Models;
using FishingLogBook.Web.Features.TestCatch.Offline;
using FishingLogBook.Web.Features.TestCatch.Pages.TestCatchLog;
using FishingLogBook.Web.Localization;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.TestCatch.Pages.TestCatchLogTests;

public class WhenTestingLoadFailure : BaseTestCatchLogTest
{
    [Fact]
    public async Task ItShouldShowLocalisedErrorAndRetry()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ITestCatchStore>();
        store.GetAllAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("read timed out"));
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<TestCatchLog>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#test-catch-load-error").TextContent.Should()
                .Contain("Could not load local catches. Your saved catches are still stored on this device.");
            cut.Find("#retry-load-test-catches-button").TextContent.Should().Contain("Retry loading");
        });
        await store.Received().GetAllAsync(Arg.Any<CancellationToken>());
        await store.DidNotReceive().SaveAsync(Arg.Any<TestCatchModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowFrenchLoadError()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        var store = Substitute.For<ITestCatchStore>();
        store.GetAllAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("read timed out"));
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<TestCatchLog>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#test-catch-load-error").TextContent.Should()
                .Contain("Impossible de charger les prises locales. Vos prises enregistrées sont toujours stockées sur cet appareil.");
            cut.Find("#retry-load-test-catches-button").TextContent.Should().Contain("Réessayer le chargement");
        });
        await store.Received().GetAllAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReloadLocalCatches_WhenRetryIsClicked()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var existing = new TestCatchModel(
            Guid.Parse("3a1f2c8e-7b44-4d1a-9c3e-2f8a6b0d1e55"),
            "Perch",
            DateTimeOffset.Parse("2026-08-14T10:30:00Z"),
            null,
            SyncStatus.SavedLocally);
        var store = Substitute.For<ITestCatchStore>();
        store.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(
                _ => Task.FromException<IReadOnlyList<TestCatchModel>>(new TimeoutException("read timed out")),
                _ => Task.FromException<IReadOnlyList<TestCatchModel>>(new TimeoutException("read timed out")),
                _ => Task.FromResult<IReadOnlyList<TestCatchModel>>([existing]));
        await using var context = CreateContext(store);
        var cut = context.Render<TestCatchLog>();
        cut.WaitForAssertion(() => cut.Find("#retry-load-test-catches-button").Should().NotBeNull());

        // Act
        await cut.Find("#retry-load-test-catches-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find($"#test-catch-item-{existing.Id}").Should().NotBeNull();
            cut.Find($"#test-catch-species-{existing.Id}").TextContent.Should().Contain("Perch");
            cut.FindAll("#test-catch-load-error").Should().BeEmpty();
        });
        await store.Received(3).GetAllAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepPreviouslyLoadedCatches_WhenALaterReadFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var existing = new TestCatchModel(
            Guid.Parse("8c0e91a2-4d77-4b18-a6f1-0c3d5e7a9b21"),
            "Pike",
            DateTimeOffset.Parse("2026-08-14T11:00:00Z"),
            null,
            SyncStatus.SavedLocally);
        var store = Substitute.For<ITestCatchStore>();
        store.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult<IReadOnlyList<TestCatchModel>>([existing]),
                Task.FromException<IReadOnlyList<TestCatchModel>>(new TimeoutException("read timed out")));
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<TestCatchLog>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find($"#test-catch-item-{existing.Id}").Should().NotBeNull();
            cut.Find("#test-catch-load-error").Should().NotBeNull();
        });
        await store.Received().GetAllAsync(Arg.Any<CancellationToken>());
        await store.DidNotReceive().SaveAsync(Arg.Any<TestCatchModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStillShowCatches_WhenPhotographReadFails()
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
            .ThrowsAsync(new TimeoutException("photograph read timed out"));
        await using var context = CreateContext(store, Substitute.For<ITestCatchSynchroniser>(), photos);

        // Act
        var cut = context.Render<TestCatchLog>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find($"#test-catch-item-{existing.Id}").Should().NotBeNull();
            cut.Find($"#test-catch-species-{existing.Id}").TextContent.Should().Contain("Carp");
            cut.Find("#test-catch-load-error").Should().NotBeNull();
            cut.Find("#retry-load-test-catches-button").Should().NotBeNull();
        });
        await store.Received().GetAllAsync(Arg.Any<CancellationToken>());
        await photos.Received().GetAsync(existing.Id, Arg.Any<CancellationToken>());
        await store.DidNotReceive().SaveAsync(Arg.Any<TestCatchModel>(), Arg.Any<CancellationToken>());
    }
}
