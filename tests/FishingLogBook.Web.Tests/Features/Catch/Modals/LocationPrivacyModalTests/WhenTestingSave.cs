using System.Net;
using System.Text;
using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Features.Catch.Clients;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Localization;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.Catch.Modals.LocationPrivacyModalTests;

public class WhenTestingSave : BaseLocationPrivacyModalTest
{
    [Fact]
    public async Task ItShouldNotSaveWhenTheCatchHasNoLocation()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var store = Substitute.For<ICatchStore>();
        store.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CatchModel>>(
                [LocatedCatch(catchId, location: null)]));
        var client = Substitute.For<ICatchClient>();
        await using var context = CreateContext(store, client);
        var (cut, dialog) = await ShowModalAsync(context, catchId);
        cut.WaitForAssertion(() => cut.Find("#catch-location-privacy-no-location").Should().NotBeNull());

        // Act
        cut.FindAll("#catch-location-privacy-save").Should().BeEmpty();

        // Assert
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
        await client.DidNotReceive().UpdateLocationVisibilityAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
        dialog.Result.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldShowThePublicWarningBeforeSaving()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var store = Substitute.For<ICatchStore>();
        store.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CatchModel>>([LocatedCatch(catchId)]));
        var client = Substitute.For<ICatchClient>();
        await using var context = CreateContext(store, client);
        var (cut, dialog) = await ShowModalAsync(context, catchId);
        cut.WaitForAssertion(() => cut.Find("#catch-location-privacy-public").Should().NotBeNull());

        // Act
        await cut.Find("#catch-location-privacy-public").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-location-privacy-public-warning").TextContent
                .Should()
                .Contain("Anyone who can view this catch may see the exact fishing spot"));
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
        await client.DidNotReceive().UpdateLocationVisibilityAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
        dialog.Result.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldNotCloseWhenTheLocalSaveFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var original = LocatedCatch(catchId);
        var store = Substitute.For<ICatchStore>();
        store.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CatchModel>>([original]));
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("IndexedDB failed."));
        var client = Substitute.For<ICatchClient>();
        await using var context = CreateContext(store, client);
        var (cut, dialog) = await ShowModalAsync(context, catchId);
        cut.WaitForAssertion(() => cut.Find("#catch-location-privacy-public").Should().NotBeNull());
        await cut.Find("#catch-location-privacy-public").ClickAsync();

        // Act
        await cut.Find("#catch-location-privacy-save").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-location-privacy-save-failed").TextContent
                .Should()
                .Contain("Location privacy could not be saved"));
        original.Location!.Visibility.Should().Be(LocationDefaults.Private);
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Id == catchId
                && catchRecord.Location != null
                && catchRecord.Location.Visibility == LocationDefaults.Public
                && catchRecord.Location.Latitude == 53.2707
                && catchRecord.Location.Longitude == -9.0568),
            Arg.Any<CancellationToken>());
        await client.DidNotReceive().UpdateLocationVisibilityAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
        dialog.Result.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldKeepTheLocalChoiceWhenTheServerIsUnavailable()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var store = Substitute.For<ICatchStore>();
        store.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CatchModel>>([LocatedCatch(catchId)]));
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var client = Substitute.For<ICatchClient>();
        client.UpdateLocationVisibilityAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException());
        await using var context = CreateContext(store, client);
        var (cut, dialog) = await ShowModalAsync(context, catchId);
        cut.WaitForAssertion(() => cut.Find("#catch-location-privacy-public").Should().NotBeNull());
        await cut.Find("#catch-location-privacy-public").ClickAsync();

        // Act
        await cut.Find("#catch-location-privacy-save").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.FindAll("#catch-location-privacy-save-failed").Should().BeEmpty());
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Id == catchId
                && catchRecord.Location != null
                && catchRecord.Location.Visibility == LocationDefaults.Public
                && catchRecord.Location.Latitude == 53.2707
                && catchRecord.Location.Longitude == -9.0568
                && catchRecord.Location.AccuracyMetres == 12
                && catchRecord.Location.Source == LocationDefaults.DeviceGps
                && catchRecord.Location.ConsentVersion == LocationDefaults.ConsentVersion),
            Arg.Any<CancellationToken>());
        await client.Received(1).UpdateLocationVisibilityAsync(
            catchId,
            LocationDefaults.Public,
            Arg.Any<CancellationToken>());
        await ShouldHaveClosedAsSaved(dialog);
    }

    [Fact]
    public async Task ItShouldKeepTheLocalChoiceWhenTheCatchHasNotSyncedYet()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var store = Substitute.For<ICatchStore>();
        store.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CatchModel>>([LocatedCatch(catchId)]));
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var apiHandler = new UnsynchronisedCatchHandler();
        var client = CreateCatchClient(apiHandler);
        await using var context = CreateContext(store, client);
        var (cut, dialog) = await ShowModalAsync(context, catchId);
        cut.WaitForAssertion(() => cut.Find("#catch-location-privacy-public").Should().NotBeNull());
        await cut.Find("#catch-location-privacy-public").ClickAsync();

        // Act
        await cut.Find("#catch-location-privacy-save").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.FindAll("#catch-location-privacy-save-failed").Should().BeEmpty());
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Id == catchId
                && catchRecord.Location != null
                && catchRecord.Location.Visibility == LocationDefaults.Public
                && catchRecord.Location.Latitude == 53.2707
                && catchRecord.Location.Longitude == -9.0568),
            Arg.Any<CancellationToken>());
        apiHandler.LastRequest.Should().NotBeNull();
        apiHandler.LastRequest!.Method.Should().Be(HttpMethod.Patch);
        apiHandler.LastRequest.RequestUri!.PathAndQuery
            .Should()
            .Be($"/api/catches/{catchId:D}/location-visibility");
        await ShouldHaveClosedAsSaved(dialog);
    }

    [Fact]
    public async Task ItShouldKeepPublicToPrivateWhenTheServerIsUnavailable()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var capturedOn = DateTimeOffset.Parse("2026-08-17T08:00:00Z");
        var store = Substitute.For<ICatchStore>();
        store.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CatchModel>>(
                [
                    LocatedCatch(catchId, LocationDefaults.Public) with
                    {
                        SyncStatus = SyncStatus.Synchronised,
                        MetadataSyncStatus = SyncStatus.Synchronised,
                        Photographs = LocatedCatch(catchId).Photographs
                            .Select(photograph => photograph with
                            {
                                SyncStatus = SyncStatus.Synchronised
                            })
                            .ToArray()
                    }
                ]));
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var client = Substitute.For<ICatchClient>();
        client.UpdateLocationVisibilityAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException());
        await using var context = CreateContext(store, client);
        var (cut, dialog) = await ShowModalAsync(context, catchId);
        cut.WaitForAssertion(() => cut.Find("#catch-location-privacy-private").Should().NotBeNull());
        await cut.Find("#catch-location-privacy-private").ClickAsync();

        // Act
        await cut.Find("#catch-location-privacy-save").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.FindAll("#catch-location-privacy-save-failed").Should().BeEmpty());
        cut.Markup.Should().NotContain("Location privacy could not be saved");
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Id == catchId
                && catchRecord.Location != null
                && catchRecord.Location.Visibility == LocationDefaults.Private
                && catchRecord.Location.Latitude == 53.2707
                && catchRecord.Location.Longitude == -9.0568
                && catchRecord.Location.AccuracyMetres == 12
                && catchRecord.Location.CapturedOn == capturedOn
                && catchRecord.Location.Source == LocationDefaults.DeviceGps
                && catchRecord.Location.ConsentVersion == LocationDefaults.ConsentVersion
                && catchRecord.SyncStatus == SyncStatus.WaitingToSynchronise
                && catchRecord.MetadataSyncStatus == SyncStatus.WaitingToSynchronise
                && catchRecord.Photographs.All(
                    photograph => photograph.SyncStatus == SyncStatus.Synchronised)),
            Arg.Any<CancellationToken>());
        await client.Received(1).UpdateLocationVisibilityAsync(
            catchId,
            LocationDefaults.Private,
            Arg.Any<CancellationToken>());
        await ShouldHaveClosedAsSaved(dialog);
    }

    [Fact]
    public async Task ItShouldCloseWithoutSavingWhenCancelled()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var store = Substitute.For<ICatchStore>();
        store.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CatchModel>>([LocatedCatch(catchId)]));
        var client = Substitute.For<ICatchClient>();
        await using var context = CreateContext(store, client);
        var (cut, dialog) = await ShowModalAsync(context, catchId);
        cut.WaitForAssertion(() => cut.Find("#catch-location-privacy-public").Should().NotBeNull());
        await cut.Find("#catch-location-privacy-public").ClickAsync();

        // Act
        await cut.Find("#catch-location-privacy-cancel").ClickAsync();

        // Assert
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
        await client.DidNotReceive().UpdateLocationVisibilityAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
        var result = await dialog.Result;
        result.Should().NotBeNull();
        result!.Canceled.Should().BeTrue();
        cut.FindAll("#catch-location-privacy-options").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldUpdateLocalVisibilityOnlyAndCloseAsSaved()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var store = Substitute.For<ICatchStore>();
        store.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CatchModel>>([LocatedCatch(catchId)]));
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var client = Substitute.For<ICatchClient>();
        client.UpdateLocationVisibilityAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        await using var context = CreateContext(store, client);
        var (cut, dialog) = await ShowModalAsync(context, catchId);
        cut.WaitForAssertion(() => cut.Find("#catch-location-privacy-public").Should().NotBeNull());
        await cut.Find("#catch-location-privacy-public").ClickAsync();

        // Act
        await cut.Find("#catch-location-privacy-save").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.FindAll("#catch-location-privacy-save-failed").Should().BeEmpty());
        cut.Markup.Should().NotContain("53.2707");
        Received.InOrder(() =>
        {
            store.SaveAsync(
                Arg.Is<CatchModel>(catchRecord =>
                    catchRecord.Id == catchId
                    && catchRecord.Location != null
                    && catchRecord.Location.Visibility == LocationDefaults.Public
                    && catchRecord.Location.Latitude == 53.2707
                    && catchRecord.Location.Longitude == -9.0568
                    && catchRecord.Location.AccuracyMetres == 12
                    && catchRecord.Location.Source == LocationDefaults.DeviceGps
                    && catchRecord.UserId == OwnerUserId
                    && catchRecord.AnglerUserId == OwnerUserId
                    && catchRecord.RecordedByUserId == OwnerUserId),
                Arg.Any<CancellationToken>());
            client.UpdateLocationVisibilityAsync(
                catchId,
                LocationDefaults.Public,
                Arg.Any<CancellationToken>());
        });
        await ShouldHaveClosedAsSaved(dialog);
    }

    [Fact]
    public async Task ItShouldShowSavedOnDeviceBeforeClosing()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var store = Substitute.For<ICatchStore>();
        store.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CatchModel>>([LocatedCatch(catchId)]));
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var client = Substitute.For<ICatchClient>();
        client.UpdateLocationVisibilityAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        await using var context = CreateContext(store, client);
        var (cut, dialog) = await ShowModalAsync(context, catchId);
        cut.WaitForAssertion(() => cut.Find("#catch-location-privacy-save").Should().NotBeNull());

        // Act
        var saving = cut.Find("#catch-location-privacy-save").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-location-privacy-saved").TextContent
                .Should()
                .Contain("Location privacy saved on this device"));
        await saving;
        await ShouldHaveClosedAsSaved(dialog);
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Id == catchId
                && catchRecord.Location != null
                && catchRecord.Location.Visibility == LocationDefaults.Private),
            Arg.Any<CancellationToken>());
        await client.Received(1).UpdateLocationVisibilityAsync(
            catchId,
            LocationDefaults.Private,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRestoreSynchronisedWhenTheServerAcceptsTheVisibility()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var store = Substitute.For<ICatchStore>();
        store.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CatchModel>>(
                [
                    LocatedCatch(catchId, LocationDefaults.Private) with
                    {
                        SyncStatus = SyncStatus.Synchronised,
                        MetadataSyncStatus = SyncStatus.Synchronised
                    }
                ]));
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var client = Substitute.For<ICatchClient>();
        client.UpdateLocationVisibilityAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        await using var context = CreateContext(store, client);
        var (cut, dialog) = await ShowModalAsync(context, catchId);
        cut.WaitForAssertion(() => cut.Find("#catch-location-privacy-public").Should().NotBeNull());
        await cut.Find("#catch-location-privacy-public").ClickAsync();

        // Act
        await cut.Find("#catch-location-privacy-save").ClickAsync();

        // Assert
        Received.InOrder(() =>
        {
            store.SaveAsync(
                Arg.Is<CatchModel>(catchRecord =>
                    catchRecord.Id == catchId
                    && catchRecord.Location != null
                    && catchRecord.Location.Visibility == LocationDefaults.Public
                    && catchRecord.SyncStatus == SyncStatus.WaitingToSynchronise
                    && catchRecord.MetadataSyncStatus == SyncStatus.WaitingToSynchronise),
                Arg.Any<CancellationToken>());
            client.UpdateLocationVisibilityAsync(
                catchId,
                LocationDefaults.Public,
                Arg.Any<CancellationToken>());
            store.SaveAsync(
                Arg.Is<CatchModel>(catchRecord =>
                    catchRecord.Id == catchId
                    && catchRecord.Location != null
                    && catchRecord.Location.Visibility == LocationDefaults.Public
                    && catchRecord.SyncStatus == SyncStatus.Synchronised
                    && catchRecord.MetadataSyncStatus == SyncStatus.Synchronised),
                Arg.Any<CancellationToken>());
        });
        await ShouldHaveClosedAsSaved(dialog);
    }

    private static CatchClient CreateCatchClient(UnsynchronisedCatchHandler apiHandler)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(HttpClientNames.AuthorizedApi)
            .Returns(new HttpClient(apiHandler) { BaseAddress = new Uri("https://api.test/") });
        factory.CreateClient(HttpClientNames.Anonymous)
            .Returns(new HttpClient { BaseAddress = new Uri("https://api.test/") });
        return new CatchClient(factory);
    }

    private sealed class UnsynchronisedCatchHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
            });
        }
    }
}
