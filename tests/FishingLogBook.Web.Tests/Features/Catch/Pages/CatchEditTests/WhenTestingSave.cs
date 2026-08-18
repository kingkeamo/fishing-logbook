using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Pages.CatchEdit;
using FishingLogBook.Web.Localization;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.CatchEditTests;

public class WhenTestingSave : BaseCatchEditTest
{
    [Fact]
    public async Task ItShouldRejectANonPositiveWeightWithoutSaving()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, catchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(catchId, SyncStatus.Synchronised, SyncStatus.Synchronised));
        var synchroniser = QuietSynchroniser();
        await using var context = CreateContext(store, synchroniser: synchroniser);
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(p => p.CatchId, catchId));
        cut.WaitForAssertion(() => cut.Find("#catch-edit-weight").Should().NotBeNull());
        cut.Find("#catch-edit-weight").Input("0");

        // Act
        await cut.Find("#catch-edit-save").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-edit-validation").TextContent
                .Should()
                .Contain("Weight must be greater than 0 kg"));
        cut.FindAll("#catch-edit-saved").Should().BeEmpty();
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
        await synchroniser.DidNotReceive().SynchronisePendingAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectAFutureCaughtOnWithoutSaving()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, catchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(catchId));
        var synchroniser = QuietSynchroniser();
        await using var context = CreateContext(store, synchroniser: synchroniser);
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(p => p.CatchId, catchId));
        cut.WaitForAssertion(() => cut.Find("#catch-edit-caught-on").Should().NotBeNull());
        cut.Find("#catch-edit-caught-on").Input(DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-ddTHH:mm"));

        // Act
        await cut.Find("#catch-edit-save").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-edit-validation").TextContent
                .Should()
                .Contain("not in the future"));
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
        await synchroniser.DidNotReceive().SynchronisePendingAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepSyncStateWhenNothingChanged()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, catchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(
                catchId,
                SyncStatus.Synchronised,
                SyncStatus.Synchronised,
                SyncStatus.Synchronised,
                "catches/photo"));
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var synchroniser = QuietSynchroniser();
        await using var context = CreateContext(store, synchroniser: synchroniser);
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(p => p.CatchId, catchId));
        cut.WaitForAssertion(() => cut.Find("#catch-edit-save").Should().NotBeNull());

        // Act
        await cut.Find("#catch-edit-save").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-edit-saved").TextContent.Should().Contain("Details saved on this device"));
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Id == catchId
                && catchRecord.SyncStatus == SyncStatus.Synchronised
                && catchRecord.MetadataSyncStatus == SyncStatus.Synchronised),
            Arg.Any<CancellationToken>());
        await synchroniser.DidNotReceive().SynchronisePendingAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSaveDetailsOnTheSameCatchAndPreservePhotographsAndProvenance()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var location = new CatchLocationModel(
            53.2707,
            -9.0568,
            12,
            DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            LocationDefaults.DeviceGps,
            LocationDefaults.Private,
            LocationDefaults.ConsentVersion);
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, catchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(
                catchId,
                SyncStatus.Synchronised,
                SyncStatus.Synchronised,
                SyncStatus.Synchronised,
                "catches/photo",
                location));
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var synchroniser = QuietSynchroniser();
        await using var context = CreateContext(store, synchroniser: synchroniser);
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(p => p.CatchId, catchId));
        cut.WaitForAssertion(() => cut.Find("#catch-edit-species").Should().NotBeNull());
        cut.Find("#catch-edit-species").Input("Pike");
        cut.Find("#catch-edit-weight").Input("2.5");
        cut.Find("#catch-edit-length").Input("64");
        cut.Find("#catch-edit-method").Input("Lure");
        cut.Find("#catch-edit-bait").Input("Spinner");
        cut.Find("#catch-edit-notes").Input("Weedline");
        cut.Find("#catch-edit-caught-on").Input("2026-08-17T09:15");

        // Act
        await cut.Find("#catch-edit-save").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-edit-saved").TextContent.Should().Contain("Details saved on this device"));
        cut.FindAll("#catch-edit-save-failed").Should().BeEmpty();
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Id == catchId
                && catchRecord.SpeciesName == "Pike"
                && catchRecord.Weight == 2.5m
                && catchRecord.Length == 64m
                && catchRecord.Method == "Lure"
                && catchRecord.BaitOrLure == "Spinner"
                && catchRecord.Notes == "Weedline"
                && catchRecord.CaughtOn == DateTimeOffset.Parse("2026-08-17T09:15:00Z")
                && catchRecord.UserId == OwnerUserId
                && catchRecord.AnglerUserId == OwnerUserId
                && catchRecord.RecordedByUserId == OwnerUserId
                && catchRecord.Location != null
                && catchRecord.Location.Latitude == 53.2707
                && catchRecord.Location.Visibility == LocationDefaults.Private
                && catchRecord.MetadataSyncStatus == SyncStatus.WaitingToSynchronise
                && catchRecord.SyncStatus == SyncStatus.WaitingToSynchronise
                && catchRecord.Photographs.Count == 1
                && catchRecord.Photographs[0].Id == Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")
                && catchRecord.Photographs[0].SyncStatus == SyncStatus.Synchronised
                && catchRecord.Photographs[0].ObjectKey == "catches/photo"),
            Arg.Any<CancellationToken>());
        await synchroniser.Received(1).SynchronisePendingAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepTheLocalEditWhenSynchronisationFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, catchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(catchId, SyncStatus.Synchronised, SyncStatus.Synchronised));
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var synchroniser = Substitute.For<ICatchSynchroniser>();
        synchroniser.SynchronisePendingAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException());
        await using var context = CreateContext(store, synchroniser: synchroniser);
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(p => p.CatchId, catchId));
        cut.WaitForAssertion(() => cut.Find("#catch-edit-species").Should().NotBeNull());
        cut.Find("#catch-edit-species").Input("Pike");

        // Act
        await cut.Find("#catch-edit-save").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-edit-saved").TextContent.Should().Contain("Details saved on this device"));
        cut.FindAll("#catch-edit-save-failed").Should().BeEmpty();
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Id == catchId
                && catchRecord.SpeciesName == "Pike"
                && catchRecord.MetadataSyncStatus == SyncStatus.WaitingToSynchronise),
            Arg.Any<CancellationToken>());
        await synchroniser.Received(1).SynchronisePendingAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowFrenchSavedCopy()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, catchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(catchId));
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var synchroniser = QuietSynchroniser();
        await using var context = CreateContext(store, synchroniser: synchroniser);
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(p => p.CatchId, catchId));
        cut.WaitForAssertion(() => cut.Find("#catch-edit-species").Should().NotBeNull());
        cut.Find("#catch-edit-species").Input("Brochet");

        // Act
        await cut.Find("#catch-edit-save").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-edit-saved").TextContent
                .Should()
                .Contain("Détails enregistrés sur cet appareil"));
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Id == catchId && catchRecord.SpeciesName == "Brochet"),
            Arg.Any<CancellationToken>());
        await synchroniser.Received(1).SynchronisePendingAsync(Arg.Any<CancellationToken>());
    }
}
