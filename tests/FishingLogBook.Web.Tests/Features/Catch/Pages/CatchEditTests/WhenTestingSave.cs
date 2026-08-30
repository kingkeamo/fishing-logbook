using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Common.Offline.Synchronisers;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
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
            .Returns(StoredCatch(catchId, SyncStatus.Synchronised, SyncStatus.Synchronised, weight: 0m));
        var synchroniser = QuietSynchroniser();
        await using var context = CreateContext(store, synchroniser: synchroniser);
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(p => p.CatchId, catchId));
        cut.WaitForAssertion(() => cut.Find("#catch-edit-weight").Should().NotBeNull());

        // Act
        await cut.Find("#catch-edit-save").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-edit-validation").TextContent
                .Should()
                .Contain("Enter a measurement greater than zero"));
        cut.FindAll("#catch-edit-saved").Should().BeEmpty();
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
        await synchroniser.DidNotReceive().SynchronisePendingAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowLoadFailedWhenTheSavedCatchCannotBeRebound()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, catchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(catchId));
        var synchroniser = QuietSynchroniser();
        var time = Substitute.For<FishingLogBook.Web.Browser.Time.ITimeService>();
        time.ToDateTimeLocalValueAsync(StoredCaughtOn, Arg.Any<CancellationToken>())
            .Returns("2026-08-17T08:00", "2026-08-17T08:00", null!);
        time.FromDateTimeLocalValueAsync("2026-08-17T08:00", Arg.Any<CancellationToken>())
            .Returns(StoredCaughtOn);
        await using var context = CreateContext(store, synchroniser: synchroniser, time: time);
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(p => p.CatchId, catchId));
        cut.WaitForAssertion(() => cut.Find("#catch-edit-save").Should().NotBeNull());
        cut.Find("#catch-edit-notes").Input("Changed before rebind failure");

        // Act
        await cut.Find("#catch-edit-save").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-edit-load-failed").TextContent.Should().Contain("could not be loaded"));
        cut.FindAll("#catch-edit-saved").Should().BeEmpty();
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord => catchRecord.Id == catchId
                && catchRecord.Notes == "Changed before rebind failure"
                && catchRecord.MetadataSyncStatus == SyncStatus.WaitingToSynchronise),
            Arg.Any<CancellationToken>());
        await synchroniser.Received(1).SynchronisePendingAsync(Arg.Any<CancellationToken>());
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
        var time = UtcTime();
        var futureLocal = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-ddTHH:mm");
        await using var context = CreateContext(store, synchroniser: synchroniser, time: time);
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(p => p.CatchId, catchId));
        cut.WaitForAssertion(() => cut.Find("#catch-edit-caught-on").Should().NotBeNull());
        cut.Find("#catch-edit-caught-on").Input(futureLocal);

        // Act
        await cut.Find("#catch-edit-save").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-edit-validation").TextContent
                .Should()
                .Contain("not in the future"));
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
        await synchroniser.DidNotReceive().SynchronisePendingAsync(Arg.Any<CancellationToken>());
        await time.Received(1).FromDateTimeLocalValueAsync(futureLocal, Arg.Any<CancellationToken>());
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
                "catch-photographs/photo"));
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var synchroniser = QuietSynchroniser();
        var time = UtcTime();
        await using var context = CreateContext(store, synchroniser: synchroniser, time: time);
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
                && catchRecord.CaughtOn == StoredCaughtOn
                && catchRecord.SyncStatus == SyncStatus.Synchronised
                && catchRecord.MetadataSyncStatus == SyncStatus.Synchronised),
            Arg.Any<CancellationToken>());
        await synchroniser.DidNotReceive().SynchronisePendingAsync(Arg.Any<CancellationToken>());
        await time.Received(1).FromDateTimeLocalValueAsync("2026-08-17T08:00", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldPreserveTheCaughtOnInstantWhenSavedUnchangedInUtcPlusFour()
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
                "catch-photographs/photo",
                caughtOn: UtcPlusFourCaughtOn));
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var synchroniser = QuietSynchroniser();
        var time = PlusFourTime();
        await using var context = CreateContext(store, synchroniser: synchroniser, time: time);
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(p => p.CatchId, catchId));
        cut.WaitForAssertion(() =>
            cut.Find("#catch-edit-caught-on").GetAttribute("value").Should().Be("2026-08-17T14:00"));

        // Act
        await cut.Find("#catch-edit-save").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-edit-saved").TextContent.Should().Contain("Details saved on this device"));
        cut.Find("#catch-edit-caught-on").GetAttribute("value").Should().Be("2026-08-17T14:00");
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Id == catchId
                && catchRecord.CaughtOn == UtcPlusFourCaughtOn
                && catchRecord.SyncStatus == SyncStatus.Synchronised
                && catchRecord.MetadataSyncStatus == SyncStatus.Synchronised),
            Arg.Any<CancellationToken>());
        await synchroniser.DidNotReceive().SynchronisePendingAsync(Arg.Any<CancellationToken>());
        await time.Received(1).FromDateTimeLocalValueAsync("2026-08-17T14:00", Arg.Any<CancellationToken>());
        await time.Received(3).ToDateTimeLocalValueAsync(
            Arg.Is<DateTimeOffset>(caughtOn => caughtOn == UtcPlusFourCaughtOn),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepSubMinuteCaughtOnWhenTheLocalDisplayIsUnchangedInUtcPlusFour()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var storedCaughtOn = DateTimeOffset.Parse("2026-08-17T10:00:31Z");
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, catchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(
                catchId,
                SyncStatus.Synchronised,
                SyncStatus.Synchronised,
                caughtOn: storedCaughtOn));
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var synchroniser = QuietSynchroniser();
        var time = PlusFourTime();
        await using var context = CreateContext(store, synchroniser: synchroniser, time: time);
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(p => p.CatchId, catchId));
        cut.WaitForAssertion(() =>
            cut.Find("#catch-edit-caught-on").GetAttribute("value").Should().Be("2026-08-17T14:00"));

        // Act
        await cut.Find("#catch-edit-save").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-edit-saved").TextContent.Should().Contain("Details saved on this device"));
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Id == catchId
                && catchRecord.CaughtOn == storedCaughtOn
                && catchRecord.MetadataSyncStatus == SyncStatus.Synchronised),
            Arg.Any<CancellationToken>());
        await synchroniser.DidNotReceive().SynchronisePendingAsync(Arg.Any<CancellationToken>());
        await time.Received(1).FromDateTimeLocalValueAsync("2026-08-17T14:00", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldConvertALocalCaughtOnCorrectionToUtcWhenTheOffsetIsUtcPlusFour()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var correctedUtc = DateTimeOffset.Parse("2026-08-17T11:00:00Z");
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, catchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(
                catchId,
                SyncStatus.Synchronised,
                SyncStatus.Synchronised,
                caughtOn: UtcPlusFourCaughtOn));
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var synchroniser = QuietSynchroniser();
        var time = PlusFourTime();
        await using var context = CreateContext(store, synchroniser: synchroniser, time: time);
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(p => p.CatchId, catchId));
        cut.WaitForAssertion(() =>
            cut.Find("#catch-edit-caught-on").GetAttribute("value").Should().Be("2026-08-17T14:00"));
        cut.Find("#catch-edit-caught-on").Input("2026-08-17T15:00");

        // Act
        await cut.Find("#catch-edit-save").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-edit-saved").TextContent.Should().Contain("Details saved on this device"));
        cut.Find("#catch-edit-caught-on").GetAttribute("value").Should().Be("2026-08-17T15:00");
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Id == catchId
                && catchRecord.CaughtOn == correctedUtc
                && catchRecord.MetadataSyncStatus == SyncStatus.WaitingToSynchronise),
            Arg.Any<CancellationToken>());
        await synchroniser.Received(1).SynchronisePendingAsync(Arg.Any<CancellationToken>());
        await time.Received(1).FromDateTimeLocalValueAsync("2026-08-17T15:00", Arg.Any<CancellationToken>());
        await time.Received(2).ToDateTimeLocalValueAsync(
            Arg.Is<DateTimeOffset>(caughtOn => caughtOn == UtcPlusFourCaughtOn),
            Arg.Any<CancellationToken>());
        await time.Received(1).ToDateTimeLocalValueAsync(
            Arg.Is<DateTimeOffset>(caughtOn => caughtOn == correctedUtc),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAcceptALocalCaughtOnThatWouldBeFutureIfTreatedAsUtcWhenTheOffsetIsUtcPlusFour()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var localClock = DateTime.SpecifyKind(
            DateTime.Parse(
                DateTime.UtcNow.AddHours(2).ToString("yyyy-MM-ddTHH:mm"),
                System.Globalization.CultureInfo.InvariantCulture),
            DateTimeKind.Unspecified);
        var localValue = localClock.ToString("yyyy-MM-ddTHH:mm");
        var expectedUtc = new DateTimeOffset(localClock.AddHours(-4), TimeSpan.Zero);
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, catchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(catchId, SyncStatus.Synchronised, SyncStatus.Synchronised));
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var synchroniser = QuietSynchroniser();
        var time = PlusFourTime();
        await using var context = CreateContext(store, synchroniser: synchroniser, time: time);
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(p => p.CatchId, catchId));
        cut.WaitForAssertion(() => cut.Find("#catch-edit-caught-on").Should().NotBeNull());
        cut.Find("#catch-edit-caught-on").Input(localValue);

        // Act
        await cut.Find("#catch-edit-save").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-edit-saved").TextContent.Should().Contain("Details saved on this device"));
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Id == catchId && catchRecord.CaughtOn == expectedUtc),
            Arg.Any<CancellationToken>());
        await synchroniser.Received(1).SynchronisePendingAsync(Arg.Any<CancellationToken>());
        await time.Received(1).FromDateTimeLocalValueAsync(localValue, Arg.Any<CancellationToken>());
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
                "catch-photographs/photo",
                location));
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var synchroniser = QuietSynchroniser();
        var time = UtcTime();
        var preferences = QuietAnglerPreferences(SamplePreferences(), SampleCatalogue());
        var modal = Substitute.For<IModalService>();
        AnswerMeasurement(modal, true, 2.5m);
        AnswerMeasurement(modal, false, 64m);
        await using var context = CreateContext(
            store,
            synchroniser: synchroniser,
            time: time,
            anglerPreferences: preferences,
            modalService: modal);
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(p => p.CatchId, catchId));
        cut.WaitForAssertion(() => cut.Find("#catch-edit-method-Spinning"));
        await cut.Find("#catch-edit-method-Spinning").ClickAsync();
        await cut.Find("#catch-edit-weight").ClickAsync();
        await cut.Find("#catch-edit-length").ClickAsync();
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
                && catchRecord.Method == "Spinning"
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
                && catchRecord.Photographs[0].ObjectKey == "catch-photographs/photo"),
            Arg.Any<CancellationToken>());
        await synchroniser.Received(1).SynchronisePendingAsync(Arg.Any<CancellationToken>());
        await time.Received(1).FromDateTimeLocalValueAsync("2026-08-17T09:15", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepTheLocalEditWhenSynchronisationFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, catchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(catchId, SyncStatus.Synchronised, SyncStatus.Synchronised, method: "Fly"));
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var synchroniser = Substitute.For<ILogbookSynchroniser>();
        synchroniser.SynchronisePendingAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException());
        var preferences = QuietAnglerPreferences(SamplePreferences(), SampleCatalogue());
        var modal = Substitute.For<IModalService>();
        AnswerMeasurement(modal, true, 1.5m);
        await using var context = CreateContext(
            store,
            synchroniser: synchroniser,
            anglerPreferences: preferences,
            modalService: modal);
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(p => p.CatchId, catchId));
        cut.WaitForAssertion(() => cut.Find("#catch-edit-method-Spinning"));
        await cut.Find("#catch-edit-method-Spinning").ClickAsync();

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
            .Returns(StoredCatch(catchId, method: "Fly"));
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var synchroniser = QuietSynchroniser();
        var preferences = QuietAnglerPreferences(SamplePreferences(), SampleCatalogue());
        await using var context = CreateContext(
            store,
            synchroniser: synchroniser,
            anglerPreferences: preferences);
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(p => p.CatchId, catchId));
        cut.WaitForAssertion(() => cut.Find("#catch-edit-species-BrownTrout"));
        await cut.Find("#catch-edit-weight").ClickAsync();

        // Act
        await cut.Find("#catch-edit-save").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-edit-saved").TextContent
                .Should()
                .Contain("Détails enregistrés sur cet appareil"));
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Id == catchId && catchRecord.SpeciesName == "Brown Trout"),
            Arg.Any<CancellationToken>());
        await synchroniser.Received(1).SynchronisePendingAsync(Arg.Any<CancellationToken>());
    }
}
