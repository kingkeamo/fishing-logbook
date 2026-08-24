using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Browser.Time;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Catch.Clients;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Pages.CatchEdit;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.CatchEditTests;

public class WhenTestingRender : BaseCatchEditTest
{
    [Fact]
    public void ItShouldRequireAnAuthenticatedUser()
    {
        // Arrange
        // Act
        var authorize = typeof(CatchEdit)
            .GetCustomAttributes(inherit: true)
            .OfType<AuthorizeAttribute>()
            .SingleOrDefault();

        // Assert
        authorize.Should().NotBeNull();
    }

    [Fact]
    public async Task ItShouldShowTheEditFields()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, catchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(catchId));
        var time = UtcTime();
        await using var context = CreateContext(store, time: time);

        // Act
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(p => p.CatchId, catchId));

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#catch-edit-title").TextContent.Should().Contain("Edit catch");
            cut.Find("#catch-edit-method-chips").Should().NotBeNull();
            cut.Find("#catch-edit-species-chips").Should().NotBeNull();
            cut.Find("#catch-edit-method-more").Should().NotBeNull();
            cut.Find("#catch-edit-species-more").Should().NotBeNull();
            cut.FindAll("#catch-edit-method").Should().BeEmpty();
            cut.FindAll("#catch-edit-species").Should().BeEmpty();
            cut.Find("#catch-edit-weight").Should().NotBeNull();
            cut.Find("#catch-edit-length").Should().NotBeNull();
            cut.Find("#catch-edit-bait").Should().NotBeNull();
            cut.Find("#catch-edit-notes").Should().NotBeNull();
            cut.Find("#catch-edit-caught-on").GetAttribute("value").Should().Be("2026-08-17T08:00");
            cut.Find("#catch-edit-save").TextContent.Should().Contain("Save details");
        });
        await store.Received(1).GetAsync(OwnerUserId, catchId, Arg.Any<CancellationToken>());
        await time.Received(1).ToDateTimeLocalValueAsync(
            Arg.Is<DateTimeOffset>(caughtOn => caughtOn == StoredCaughtOn),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowCaughtOnInDeviceLocalTimeWhenTheOffsetIsUtcPlusFour()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, catchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(catchId, caughtOn: UtcPlusFourCaughtOn));
        var time = PlusFourTime();
        await using var context = CreateContext(store, time: time);

        // Act
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(p => p.CatchId, catchId));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-edit-caught-on").GetAttribute("value").Should().Be("2026-08-17T14:00"));
        await store.Received(1).GetAsync(OwnerUserId, catchId, Arg.Any<CancellationToken>());
        await time.Received(1).ToDateTimeLocalValueAsync(
            Arg.Is<DateTimeOffset>(caughtOn => caughtOn == UtcPlusFourCaughtOn),
            Arg.Any<CancellationToken>());
        await time.DidNotReceive().FromDateTimeLocalValueAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowFrenchCopy()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, catchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(catchId));
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(p => p.CatchId, catchId));

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#catch-edit-title").TextContent.Should().Contain("Modifier la prise");
            cut.Find("#catch-edit-save").TextContent.Should().Contain("Enregistrer les détails");
        });
        await store.Received(1).GetAsync(OwnerUserId, catchId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowLoadFailedWhenTheCatchIsMissing()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, catchId, Arg.Any<CancellationToken>())
            .Returns((CatchModel?)null);
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(p => p.CatchId, catchId));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-edit-load-failed").TextContent.Should().Contain("could not be loaded"));
        cut.FindAll("#catch-edit-save").Should().BeEmpty();
        await store.Received(1).GetAsync(OwnerUserId, catchId, Arg.Any<CancellationToken>());
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowLoadFailedWhenTheCaughtOnCannotBeBound()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, catchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(catchId));
        var time = Substitute.For<ITimeService>();
        time.ToDateTimeLocalValueAsync(StoredCaughtOn, Arg.Any<CancellationToken>())
            .Returns((string)null!);
        await using var context = CreateContext(store, time: time);

        // Act
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(p => p.CatchId, catchId));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-edit-load-failed").TextContent.Should().Contain("could not be loaded"));
        cut.FindAll("#catch-edit-caught-on").Should().BeEmpty();
        cut.FindAll("#catch-edit-save").Should().BeEmpty();
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotLoadAnotherUsersCatch()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OtherUserId, catchId, Arg.Any<CancellationToken>())
            .Returns((CatchModel?)null);
        var owner = Substitute.For<ILocalCatchOwnerService>();
        owner.GetUserIdAsync(Arg.Any<CancellationToken>()).Returns(OtherUserId);
        await using var context = CreateContext(store, owner);

        // Act
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(p => p.CatchId, catchId));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-edit-load-failed").TextContent.Should().Contain("could not be loaded"));
        await owner.Received(1).GetUserIdAsync(Arg.Any<CancellationToken>());
        await store.Received(1).GetAsync(OtherUserId, catchId, Arg.Any<CancellationToken>());
        await store.DidNotReceive().GetAsync(OwnerUserId, catchId, Arg.Any<CancellationToken>());
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldLocalizeTheCatchFromTheServerWhenNotSavedLocally()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var photographId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, catchId, Arg.Any<CancellationToken>())
            .Returns((CatchModel?)null);
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var catchClient = Substitute.For<ICatchClient>();
        catchClient.GetAsync(catchId, Arg.Any<CancellationToken>())
            .Returns(new CatchViewDto(catchId, OwnerUserId, StoredCaughtOn)
            {
                AnglerUserId = OwnerUserId,
                RecordedByUserId = OwnerUserId,
                SpeciesName = "Pike",
                Photographs = [new CatchPhotographViewDto(photographId, PhotographContentTypeConstants.Jpeg, "https://r2.test/one")]
            });
        catchClient.DownloadPhotographAsync("https://r2.test/one", Arg.Any<CancellationToken>())
            .Returns(new byte[] { 1, 2, 3 });
        await using var context = CreateContext(store, catchClient: catchClient);

        // Act
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(p => p.CatchId, catchId));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-edit-species-Pike").ClassList.Should().Contain("mud-chip-filled"));
        await catchClient.Received(1).DownloadPhotographAsync("https://r2.test/one", Arg.Any<CancellationToken>());
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Id == catchId
                && catchRecord.SyncStatus == SyncStatus.Synchronised
                && catchRecord.Photographs.Single().Id == photographId
                && catchRecord.Photographs.Single().Bytes!.SequenceEqual(new byte[] { 1, 2, 3 })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowAnOfflineSpecificMessageWhenTheCatchCannotBeDownloaded()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, catchId, Arg.Any<CancellationToken>())
            .Returns((CatchModel?)null);
        var catchClient = Substitute.For<ICatchClient>();
        var logging = QuietLogging();
        var failure = new HttpRequestException("offline");
        catchClient.GetAsync(catchId, Arg.Any<CancellationToken>())
            .ThrowsAsync(failure);
        await using var context = CreateContext(store, logging: logging, catchClient: catchClient);

        // Act
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(p => p.CatchId, catchId));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-edit-load-failed").TextContent
                .Should().Contain("isn't saved on this device yet"));
        await logging.Received(1).LogErrorAsync(
            "loading a server catch for local editing",
            Arg.Is<Exception>(exception => ReferenceEquals(exception, failure)),
            Arg.Any<CancellationToken>());
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotLocalizeACatchOwnedByAnotherUser()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, catchId, Arg.Any<CancellationToken>())
            .Returns((CatchModel?)null);
        var catchClient = Substitute.For<ICatchClient>();
        catchClient.GetAsync(catchId, Arg.Any<CancellationToken>())
            .Returns(new CatchViewDto(catchId, OtherUserId, StoredCaughtOn)
            {
                AnglerUserId = OtherUserId,
                RecordedByUserId = OtherUserId,
                Photographs = [new CatchPhotographViewDto(Guid.NewGuid(), PhotographContentTypeConstants.Jpeg, "https://r2.test/one")]
            });
        await using var context = CreateContext(store, catchClient: catchClient);

        // Act
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(p => p.CatchId, catchId));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-edit-load-failed").TextContent.Should().Contain("could not be loaded"));
        await catchClient.DidNotReceive().DownloadPhotographAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
    }
}

