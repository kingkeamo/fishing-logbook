using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Browser.Location;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Catch.Clients;
using FishingLogBook.Web.Features.Catch.Components.RecordCatchEditor;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Offline.Synchronisers;
using FishingLogBook.Web.Features.Catch.Pages.RecordCatch;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Profile.Clients;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.RecordCatchTests;

public class WhenTestingSave : BaseRecordCatchTest
{
    [Fact]
    public void ItShouldRequireAnAuthenticatedUser()
    {
        // Arrange
        // Act
        var authorize = typeof(RecordCatch)
            .GetCustomAttributes(inherit: true)
            .OfType<AuthorizeAttribute>()
            .SingleOrDefault();

        // Assert
        authorize.Should().NotBeNull();
    }

    [Fact]
    public async Task ItShouldDisableSaveUntilAPhotographExists()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        var synchroniser = QuietSynchroniser();
        await using var context = CreateContext(store, synchroniser: synchroniser);

        // Act
        var cut = context.Render<RecordCatch>();

        // Assert
        cut.Find("#save-catch-button").HasAttribute("disabled").Should().BeTrue();
        cut.FindAll("#catch-photo-carousel").Should().BeEmpty();
        cut.FindAll("#catch-species").Should().BeEmpty();
        cut.FindAll("#catch-weight").Should().BeEmpty();
        cut.FindAll("#catch-length").Should().BeEmpty();
        cut.FindAll("#catch-notes").Should().BeEmpty();
        cut.FindAll("#catch-location").Should().BeEmpty();
        cut.FindAll("#catch-location-status").Should().BeEmpty();
        cut.FindAll("#test-catch-location-explainer").Should().BeEmpty();
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
        await synchroniser.DidNotReceive().SynchronisePendingAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotSaveWhenSaveIsDisabled()
    {
        // Arrange
        var store = Substitute.For<ICatchStore>();
        var synchroniser = QuietSynchroniser();
        await using var context = CreateContext(store, synchroniser: synchroniser);
        var cut = context.Render<RecordCatch>();

        // Act
        await cut.Find("#save-catch-button").ClickAsync();

        // Assert
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
        await synchroniser.DidNotReceive().SynchronisePendingAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotShowSuccessWhenLocalSaveFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Photograph persistence failed."));
        var synchroniser = QuietSynchroniser();
        await using var context = CreateContext(store, synchroniser: synchroniser);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("catch.jpg", 0xFF, 0xD8, 0xFF));

        // Act
        await cut.Find("#save-catch-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-save-failed").TextContent.Should().Contain("could not be saved"));
        cut.Find("#catch-photo-carousel").Should().NotBeNull();
        cut.FindAll("#catch-saved").Should().BeEmpty();
        cut.Find("#save-catch-button").Should().NotBeNull();
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord => catchRecord.UserId == OwnerUserId &&
                catchRecord.Id != Guid.Empty
                && catchRecord.Photographs.Count == 1
                && catchRecord.Photographs[0].Id != Guid.Empty
                && catchRecord.SpeciesName == null
                && catchRecord.CaughtOn.Offset == TimeSpan.Zero
                && catchRecord.Location == null),
            Arg.Any<CancellationToken>());
        await synchroniser.DidNotReceive().SynchronisePendingAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepTheSavedCatchVisibleAndReadOnly()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var synchroniser = QuietSynchroniser();
        await using var context = CreateContext(store, synchroniser: synchroniser);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("catch.jpg", 0xFF, 0xD8, 0xFF));
        var photographId = VisiblePhotographId(cut);
        var caughtOn = cut.Find("#catch-caught-on").TextContent;

        // Act
        await cut.Find("#save-catch-button").ClickAsync();

        // Assert
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord => catchRecord.UserId == OwnerUserId &&
                catchRecord.AnglerUserId == OwnerUserId &&
                catchRecord.RecordedByUserId == OwnerUserId &&
                catchRecord.Photographs.Count == 1
                && catchRecord.Photographs[0].Id == photographId
                && catchRecord.Photographs[0].CatchId == catchRecord.Id
                && catchRecord.SpeciesName == null
                && catchRecord.CaughtOn != default
                && catchRecord.CaughtOn.Offset == TimeSpan.Zero
                && catchRecord.Location == null),
            Arg.Any<CancellationToken>());
        cut.WaitForAssertion(() =>
        {
            cut.Find("#catch-saved").TextContent.Should().Contain("Catch saved on this device");
            cut.Find("#catch-photo-carousel").Should().NotBeNull();
            VisiblePhotographId(cut).Should().Be(photographId);
            cut.Find("#catch-caught-on").TextContent.Should().Be(caughtOn);
            cut.Find("#catch-record-another").Should().NotBeNull();
            cut.Find("#catch-view-catches").Should().NotBeNull();
            cut.Find("#catch-view-catches").GetAttribute("href").Should().Be("/catches");
        });
        cut.FindAll("#save-catch-button").Should().BeEmpty();
        cut.FindAll("#catch-photo-remove").Should().BeEmpty();
        cut.FindAll("#catch-take-photo").Should().BeEmpty();
        cut.FindAll("#catch-choose-photo").Should().BeEmpty();
        cut.FindAll("#catch-location-saved").Should().BeEmpty();
        cut.FindAll("#catch-location-status").Should().BeEmpty();
        cut.FindAll("#catch-location").Should().BeEmpty();
        cut.FindComponents<InputFile>().Should().BeEmpty();
        await synchroniser.Received(1).SynchronisePendingAsync(Arg.Any<CancellationToken>());
        await synchroniser.DidNotReceive().RetryAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSaveLocallyWithoutSpeciesAndShowFrenchCopy()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        await using var context = CreateContext(store);
        var cut = context.Render<RecordCatch>();
        cut.Find("#save-catch-button").TextContent.Should().Contain("Enregistrer");
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("prise.jpg", 0xFF, 0xD8, 0xFF));

        // Act
        await cut.Find("#save-catch-button").ClickAsync();

        // Assert
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord => catchRecord.UserId == OwnerUserId &&
                catchRecord.Photographs.Count == 1
                && catchRecord.Photographs[0].Bytes != null
                && catchRecord.SpeciesName == null
                && catchRecord.CaughtOn != default
                && catchRecord.CaughtOn.Offset == TimeSpan.Zero),
            Arg.Any<CancellationToken>());
        cut.WaitForAssertion(() =>
        {
            cut.Find("#catch-saved").TextContent.Should().Contain("Prise enregistrée sur cet appareil");
            cut.Find("#catch-photo-carousel").Should().NotBeNull();
            cut.Find("#catch-record-another").TextContent.Should().Contain("Enregistrer une autre prise");
            cut.Find("#catch-view-catches").TextContent.Should().Contain("Voir les prises");
        });
        cut.FindAll("#save-catch-button").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldClearTheSavedCatchWhenRecordingAnother()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        await using var context = CreateContext(store);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("catch.jpg", 0xFF, 0xD8, 0xFF));
        await cut.Find("#save-catch-button").ClickAsync();
        cut.WaitForAssertion(() => cut.Find("#catch-record-another").Should().NotBeNull());

        // Act
        await cut.Find("#catch-record-another").ClickAsync();

        // Assert
        cut.FindAll("#catch-photo-carousel").Should().BeEmpty();
        cut.FindAll("#catch-caught-on").Should().BeEmpty();
        cut.FindAll("#catch-saved").Should().BeEmpty();
        cut.Find("#save-catch-button").HasAttribute("disabled").Should().BeTrue();
        cut.Find("#catch-take-photo").Should().NotBeNull();
        cut.Find("#catch-choose-photo").Should().NotBeNull();
        await store.Received(1).SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSaveTwoCatchesWithDistinctIdsWithoutCallingTheNetwork()
    {
        // Arrange
        var saved = new List<CatchModel>();
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                saved.Add(call.ArgAt<CatchModel>(0));
                return Task.CompletedTask;
            });
        var synchroniser = QuietSynchroniser();
        await using var context = CreateContext(store, synchroniser: synchroniser);
        var cut = context.Render<RecordCatch>();

        // Act
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("first.jpg", 0xFF, 0xD8, 0xFF));
        await cut.Find("#save-catch-button").ClickAsync();
        cut.WaitForAssertion(() => cut.Find("#catch-record-another").Should().NotBeNull());
        await cut.Find("#catch-record-another").ClickAsync();
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("second.jpg", 0xFF, 0xD8, 0xFE));
        await cut.Find("#save-catch-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => saved.Should().HaveCount(2));
        saved[0].Id.Should().NotBe(saved[1].Id);
        saved[0].Photographs[0].Id.Should().NotBe(saved[1].Photographs[0].Id);
        saved[0].Photographs[0].Id.Should().NotBe(saved[0].Id);
        saved.Should().OnlyContain(catchRecord => catchRecord.SpeciesName == null);
        await store.Received(2).SaveAsync(
            Arg.Is<CatchModel>(catchRecord => catchRecord.UserId == OwnerUserId &&
                catchRecord.Id != Guid.Empty
                && catchRecord.Photographs.Count == 1
                && catchRecord.Photographs[0].Bytes != null),
            Arg.Any<CancellationToken>());
        await synchroniser.Received(2).SynchronisePendingAsync(Arg.Any<CancellationToken>());
        await synchroniser.DidNotReceive().RetryAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ItShouldDependOnlyOnLocalPersistence()
    {
        // Arrange
        // Act
        var injected = new[] { typeof(RecordCatch), typeof(RecordCatchEditor) }
            .SelectMany(type => type
            .GetProperties(System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic)
            .Where(property => property.GetCustomAttributes(typeof(Microsoft.AspNetCore.Components.InjectAttribute), true).Length > 0)
            .Select(property => property.PropertyType))
            .ToArray();

        // Assert
        injected.Should().Contain(typeof(ICatchStore));
        injected.Should().Contain(typeof(ILocationService));
        injected.Should().Contain(typeof(ILocalCatchOwnerService));
        injected.Should().Contain(typeof(ICatchSynchroniser));
        injected.Should().Contain(typeof(ILoggingService));
        injected.Should().NotContain(typeof(HttpClient));
        injected.Should().NotContain(typeof(ICatchClient));
        injected.Should().NotContain(type =>
            type.Name.Contains("Client", StringComparison.Ordinal)
            && type != typeof(IFishingPreferenceClient));
    }

    [Fact]
    public async Task ItShouldNotSaveWhenTheOwnerCannotBeResolved()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        var owner = Substitute.For<ILocalCatchOwnerService>();
        owner.GetUserIdAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("The current user is not signed in."));
        var synchroniser = QuietSynchroniser();
        await using var context = CreateContext(store, owner: owner, synchroniser: synchroniser);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("catch.jpg", 0xFF, 0xD8, 0xFF));

        // Act
        await cut.Find("#save-catch-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-save-failed").TextContent.Should().Contain("could not be saved"));
        cut.FindAll("#catch-saved").Should().BeEmpty();
        await owner.Received(1).GetUserIdAsync(Arg.Any<CancellationToken>());
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
        await synchroniser.DidNotReceive().SynchronisePendingAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepLocalSaveSuccessWhenSyncFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var synchroniser = Substitute.For<ICatchSynchroniser>();
        synchroniser.SynchronisePendingAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("The API is unavailable."));
        var logging = QuietLogging();
        await using var context = CreateContext(store, synchroniser: synchroniser, logging: logging);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("catch.jpg", 0xFF, 0xD8, 0xFF));

        // Act
        await cut.Find("#save-catch-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#catch-saved").TextContent.Should().Contain("Catch saved on this device");
            cut.FindAll("#catch-save-failed").Should().BeEmpty();
        });
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord => catchRecord.UserId == OwnerUserId &&
                catchRecord.SyncStatus == SyncStatus.SavedLocally),
            Arg.Any<CancellationToken>());
        await synchroniser.Received(1).SynchronisePendingAsync(Arg.Any<CancellationToken>());
        await logging.Received(1).LogErrorAsync(
            "catch synchronisation",
            Arg.Is<Exception>(exception =>
                exception is HttpRequestException
                && exception.Message == "The API is unavailable."),
            Arg.Any<CancellationToken>());
        await logging.DidNotReceive().LogErrorAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldLogUnexpectedSyncExceptionsWithoutFailingLocalSave()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var synchroniser = Substitute.For<ICatchSynchroniser>();
        synchroniser.SynchronisePendingAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("IndexedDB getAll failed."));
        var logging = QuietLogging();
        await using var context = CreateContext(store, synchroniser: synchroniser, logging: logging);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("catch.jpg", 0xFF, 0xD8, 0xFF));

        // Act
        await cut.Find("#save-catch-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#catch-saved").TextContent.Should().Contain("Catch saved on this device");
            cut.FindAll("#catch-save-failed").Should().BeEmpty();
            cut.FindAll("#save-catch-button").Should().BeEmpty();
        });
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord => catchRecord.UserId == OwnerUserId &&
                catchRecord.SyncStatus == SyncStatus.SavedLocally),
            Arg.Any<CancellationToken>());
        await synchroniser.Received(1).SynchronisePendingAsync(Arg.Any<CancellationToken>());
        await logging.Received(1).LogErrorAsync(
            "catch synchronisation",
            Arg.Is<Exception>(exception =>
                exception is InvalidOperationException
                && exception.Message == "IndexedDB getAll failed."),
            Arg.Any<CancellationToken>());
        await logging.DidNotReceive().LogErrorAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
        await synchroniser.DidNotReceive().RetryAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepLocalSaveSuccessWhenSyncHangs()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var synchroniser = Substitute.For<ICatchSynchroniser>();
        synchroniser.SynchronisePendingAsync(Arg.Any<CancellationToken>())
            .Returns(Hang());
        await using var context = CreateContext(store, synchroniser: synchroniser);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("catch.jpg", 0xFF, 0xD8, 0xFF));

        // Act
        await cut.Find("#save-catch-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#catch-saved").TextContent.Should().Contain("Catch saved on this device");
            cut.FindAll("#catch-save-failed").Should().BeEmpty();
            cut.FindAll("#save-catch-button").Should().BeEmpty();
        });
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord => catchRecord.UserId == OwnerUserId),
            Arg.Any<CancellationToken>());
        await synchroniser.Received(1).SynchronisePendingAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldTriggerProductionSyncAfterLocalSaveCompletes()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var order = new List<string>();
        var store = Substitute.For<ICatchStore>();
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                await Task.Yield();
                order.Add("save");
            });
        var synchroniser = Substitute.For<ICatchSynchroniser>();
        synchroniser.SynchronisePendingAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                order.Add("sync");
                return Task.CompletedTask;
            });
        var logging = QuietLogging();
        await using var context = CreateContext(store, synchroniser: synchroniser, logging: logging);
        var cut = context.Render<RecordCatch>();
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("catch.jpg", 0xFF, 0xD8, 0xFF));

        // Act
        await cut.Find("#save-catch-button").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#catch-saved").TextContent.Should().Contain("Catch saved on this device");
            cut.FindAll("#catch-save-failed").Should().BeEmpty();
            order.Should().Equal("save", "sync");
        });
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord => catchRecord.UserId == OwnerUserId &&
                catchRecord.AnglerUserId == OwnerUserId &&
                catchRecord.RecordedByUserId == OwnerUserId &&
                catchRecord.SyncStatus == SyncStatus.SavedLocally),
            Arg.Any<CancellationToken>());
        await synchroniser.Received(1).SynchronisePendingAsync(Arg.Any<CancellationToken>());
        await synchroniser.DidNotReceive().RetryAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await logging.DidNotReceive().LogErrorAsync(
            Arg.Any<string>(),
            Arg.Any<Exception>(),
            Arg.Any<CancellationToken>());
        await logging.DidNotReceive().LogErrorAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    private static Task Hang()
    {
        return new TaskCompletionSource().Task;
    }
}
