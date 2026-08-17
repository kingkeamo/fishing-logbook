using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Pages.CatchLocationPrivacy;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Localization;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.CatchLocationPrivacyTests;

public class WhenTestingSave : BaseCatchLocationPrivacyTest
{
    [Fact]
    public async Task ItShouldNotSaveWhenTheCatchHasNoLocation()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var store = Substitute.For<ICatchStore>();
        store.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CatchModel>>(
                [LocatedCatch(catchId, location: null)]));
        var client = Substitute.For<ICatchClient>();
        await using var context = CreateContext(store, client);
        var cut = context.Render<CatchLocationPrivacy>(parameters => parameters.Add(p => p.CatchId, catchId));
        cut.WaitForAssertion(() => cut.Find("#catch-location-privacy-no-location").Should().NotBeNull());

        // Act
        cut.FindAll("#catch-location-privacy-save").Should().BeEmpty();

        // Assert
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
        await client.DidNotReceive().UpdateLocationVisibilityAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowThePublicWarningBeforeSaving()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var store = Substitute.For<ICatchStore>();
        store.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CatchModel>>([LocatedCatch(catchId)]));
        var client = Substitute.For<ICatchClient>();
        await using var context = CreateContext(store, client);
        var cut = context.Render<CatchLocationPrivacy>(parameters => parameters.Add(p => p.CatchId, catchId));
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
    }

    [Fact]
    public async Task ItShouldShowSaveFailedWhenTheStoreFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var store = Substitute.For<ICatchStore>();
        store.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CatchModel>>([LocatedCatch(catchId)]));
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("IndexedDB failed."));
        var client = Substitute.For<ICatchClient>();
        await using var context = CreateContext(store, client);
        var cut = context.Render<CatchLocationPrivacy>(parameters => parameters.Add(p => p.CatchId, catchId));
        cut.WaitForAssertion(() => cut.Find("#catch-location-privacy-save").Should().NotBeNull());

        // Act
        await cut.Find("#catch-location-privacy-save").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-location-privacy-save-failed").TextContent
                .Should()
                .Contain("Location privacy could not be saved"));
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Id == catchId
                && catchRecord.Location != null
                && catchRecord.Location.Visibility == LocationDefaults.Private
                && catchRecord.Location.Latitude == 53.2707
                && catchRecord.Location.Longitude == -9.0568),
            Arg.Any<CancellationToken>());
        await client.DidNotReceive().UpdateLocationVisibilityAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowSaveFailedWhenTheApiClientFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var store = Substitute.For<ICatchStore>();
        store.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CatchModel>>([LocatedCatch(catchId)]));
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var client = Substitute.For<ICatchClient>();
        client.UpdateLocationVisibilityAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException());
        await using var context = CreateContext(store, client);
        var cut = context.Render<CatchLocationPrivacy>(parameters => parameters.Add(p => p.CatchId, catchId));
        cut.WaitForAssertion(() => cut.Find("#catch-location-privacy-save").Should().NotBeNull());

        // Act
        await cut.Find("#catch-location-privacy-save").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-location-privacy-save-failed").TextContent
                .Should()
                .Contain("Location privacy could not be saved"));
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Id == catchId
                && catchRecord.Location != null
                && catchRecord.Location.Visibility == LocationDefaults.Private
                && catchRecord.Location.Latitude == 53.2707),
            Arg.Any<CancellationToken>());
        await client.Received(1).UpdateLocationVisibilityAsync(
            catchId,
            LocationDefaults.Private,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldUpdateLocalVisibilityOnlyAndPatchTheServer()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var store = Substitute.For<ICatchStore>();
        store.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CatchModel>>([LocatedCatch(catchId)]));
        store.SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var client = Substitute.For<ICatchClient>();
        client.UpdateLocationVisibilityAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        await using var context = CreateContext(store, client);
        var cut = context.Render<CatchLocationPrivacy>(parameters => parameters.Add(p => p.CatchId, catchId));
        cut.WaitForAssertion(() => cut.Find("#catch-location-privacy-public").Should().NotBeNull());
        await cut.Find("#catch-location-privacy-public").ClickAsync();

        // Act
        await cut.Find("#catch-location-privacy-save").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-location-privacy-saved").TextContent.Should().Contain("Location privacy saved"));
        cut.Markup.Should().NotContain("53.2707");
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(catchRecord =>
                catchRecord.Id == catchId
                && catchRecord.Location != null
                && catchRecord.Location.Visibility == LocationDefaults.Public
                && catchRecord.Location.Latitude == 53.2707
                && catchRecord.Location.Longitude == -9.0568
                && catchRecord.Location.AccuracyMetres == 12
                && catchRecord.Location.Source == LocationDefaults.DeviceGps),
            Arg.Any<CancellationToken>());
        await client.Received(1).UpdateLocationVisibilityAsync(
            catchId,
            LocationDefaults.Public,
            Arg.Any<CancellationToken>());
    }
}
