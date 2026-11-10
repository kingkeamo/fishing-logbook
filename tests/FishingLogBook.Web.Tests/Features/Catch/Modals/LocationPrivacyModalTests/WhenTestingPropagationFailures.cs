using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Catch.Clients;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Localization;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Modals.LocationPrivacyModalTests;

public class WhenTestingPropagationFailures : BaseLocationPrivacyModalTest
{
    private static readonly Guid CatchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task ItShouldLogAndStayOpenWhenTheServerFailsInAnUnexpectedWay()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, CatchId, Arg.Any<CancellationToken>()).Returns(LocatedCatch(CatchId));
        var client = Substitute.For<ICatchClient>();
        client.UpdateLocationVisibilityAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("unexpected")));
        var logging = QuietLogging();
        await using var context = CreateContext(store, client, logging: logging);
        var (cut, dialog) = await ShowModalAsync(context, CatchId);
        cut.WaitForAssertion(() => cut.Find("#catch-location-privacy-public").Should().NotBeNull());
        await cut.Find("#catch-location-privacy-public").ClickAsync();

        // Act
        await cut.Find("#catch-location-privacy-save").ClickAsync();

        // Assert
        dialog.Result.IsCompleted.Should().BeFalse();
        await logging.Received(1).LogErrorAsync(
            "saving catch location privacy",
            Arg.Is<Exception>(exception => exception.Message == "unexpected"),
            CancellationToken.None);
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(saved => saved.Location != null && saved.Location.Visibility == LocationDefaults.Public),
            Arg.Any<CancellationToken>());
        cut.FindAll("#catch-location-privacy-save-failed").Should().BeEmpty();
    }

    [Theory]
    [InlineData(typeof(TaskCanceledException))]
    [InlineData(typeof(TimeoutException))]
    public async Task ItShouldQueueTheServerUpdateWhenTheRequestTimesOut(Type failureType)
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, CatchId, Arg.Any<CancellationToken>()).Returns(LocatedCatch(CatchId) with
        {
            SyncStatus = SyncStatus.Synchronised,
            MetadataSyncStatus = SyncStatus.Synchronised
        });
        var client = Substitute.For<ICatchClient>();
        client.UpdateLocationVisibilityAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException((Exception)Activator.CreateInstance(failureType)!));
        await using var context = CreateContext(store, client);
        var (cut, dialog) = await ShowModalAsync(context, CatchId);
        cut.WaitForAssertion(() => cut.Find("#catch-location-privacy-public").Should().NotBeNull());
        await cut.Find("#catch-location-privacy-public").ClickAsync();

        // Act
        await cut.Find("#catch-location-privacy-save").ClickAsync();

        // Assert
        await ShouldHaveClosedAsSaved(dialog);
        await store.Received(2).SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(saved => saved.SyncStatus == SyncStatus.WaitingToSynchronise
                && saved.MetadataSyncStatus == SyncStatus.WaitingToSynchronise),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldOnlyQueueTheMetadataWhenOnlyTheMetadataWasSynchronised()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, CatchId, Arg.Any<CancellationToken>()).Returns(LocatedCatch(CatchId) with
        {
            SyncStatus = SyncStatus.SavedLocally,
            MetadataSyncStatus = SyncStatus.Synchronised
        });
        var client = Substitute.For<ICatchClient>();
        client.UpdateLocationVisibilityAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new HttpRequestException("offline")));
        await using var context = CreateContext(store, client);
        var (cut, dialog) = await ShowModalAsync(context, CatchId);
        cut.WaitForAssertion(() => cut.Find("#catch-location-privacy-public").Should().NotBeNull());
        await cut.Find("#catch-location-privacy-public").ClickAsync();

        // Act
        await cut.Find("#catch-location-privacy-save").ClickAsync();

        // Assert
        await ShouldHaveClosedAsSaved(dialog);
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(saved => saved.SyncStatus == SyncStatus.SavedLocally
                && saved.MetadataSyncStatus == SyncStatus.WaitingToSynchronise),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowTheLoadFailureWhenTheCatchCannotBeFound()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, CatchId, Arg.Any<CancellationToken>()).Returns((CatchModel?)null);
        var client = Substitute.For<ICatchClient>();
        await using var context = CreateContext(store, client);

        // Act
        var (cut, _) = await ShowModalAsync(context, CatchId);

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-location-privacy-load-failed").Should().NotBeNull());
        cut.FindAll("#catch-location-privacy-save").Should().BeEmpty();
        await store.Received(1).GetAsync(OwnerUserId, CatchId, Arg.Any<CancellationToken>());
    }
}
