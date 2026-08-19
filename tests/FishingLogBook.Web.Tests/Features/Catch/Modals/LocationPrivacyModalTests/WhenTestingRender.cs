using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Catch.Clients;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Localization;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.Catch.Modals.LocationPrivacyModalTests;

public class WhenTestingRender : BaseLocationPrivacyModalTest
{
    [Fact]
    public async Task ItShouldShowTheNoLocationMessageWhenTheCatchHasNoCoordinates()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var store = Substitute.For<ICatchStore>();
        store.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CatchModel>>([LocatedCatch(catchId, location: null)]));
        var client = Substitute.For<ICatchClient>();
        await using var context = CreateContext(store, client);

        // Act
        var (cut, dialog) = await ShowModalAsync(context, catchId);

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-location-privacy-no-location").TextContent
                .Should()
                .Contain("This catch has no saved location"));
        cut.FindAll("#catch-location-privacy-options").Should().BeEmpty();
        cut.Find("#catch-location-privacy-cancel").TextContent.Should().Contain("Cancel");
        await store.Received(1).GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>());
        await client.DidNotReceive().UpdateLocationVisibilityAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
        dialog.Result.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldShowEnglishVisibilityOptionsWithoutRawCoordinates()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var store = Substitute.For<ICatchStore>();
        store.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CatchModel>>([LocatedCatch(catchId)]));
        await using var context = CreateContext(store);

        // Act
        var (cut, _) = await ShowModalAsync(context, catchId);

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#catch-location-privacy-title").TextContent.Should().Contain("Location privacy");
            cut.Find("#catch-location-privacy-private").Should().NotBeNull();
            cut.Find("#catch-location-privacy-approximate").Should().NotBeNull();
            cut.Find("#catch-location-privacy-venue").Should().NotBeNull();
            cut.Find("#catch-location-privacy-public").Should().NotBeNull();
            var options = cut.Find("#catch-location-privacy-options").TextContent;
            options.Should().Contain("Only me");
            options.Should().Contain("Only you can see the exact location.");
            options.Should().Contain("Approximate area");
            options.Should().Contain("Others can see a general area, not the exact spot.");
            options.Should().Contain("Fishing venue only");
            options.Should().Contain("Others can see the associated venue, not the precise coordinates.");
            options.Should().Contain("Public exact location");
            options.Should().Contain("The precise location may be visible publicly.");
        });
        cut.FindAll("#catch-location-privacy-public-warning").Should().BeEmpty();
        cut.Find("#catch-location-privacy-cancel").TextContent.Should().Contain("Cancel");
        cut.Markup.Should().NotContain("53.2707");
        cut.Markup.Should().NotContain("-9.0568");
        await store.Received(1).GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowFrenchVisibilityOptions()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        var catchId = Guid.NewGuid();
        var store = Substitute.For<ICatchStore>();
        store.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CatchModel>>([LocatedCatch(catchId)]));
        await using var context = CreateContext(store);

        // Act
        var (cut, _) = await ShowModalAsync(context, catchId);

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#catch-location-privacy-title").TextContent
                .Should()
                .Contain("Confidentialité de la localisation");
            var options = cut.Find("#catch-location-privacy-options").TextContent;
            options.Should().Contain("Moi uniquement");
            options.Should().Contain("Seul vous pouvez voir l'emplacement exact.");
            options.Should().Contain("Zone approximative");
            options.Should().Contain("Les autres peuvent voir une zone générale, pas l'endroit exact.");
            options.Should().Contain("Site de pêche uniquement");
            options.Should().Contain("Les autres peuvent voir le site associé, pas les coordonnées précises.");
            options.Should().Contain("Position exacte publique");
            options.Should().Contain("L'emplacement précis peut être visible publiquement.");
            cut.Find("#catch-location-privacy-cancel").TextContent.Should().Contain("Annuler");
        });
        await store.Received(1).GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowLoadFailedCopyWhenTheStoreFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("IndexedDB failed."));
        await using var context = CreateContext(store);

        // Act
        var (cut, dialog) = await ShowModalAsync(context, Guid.NewGuid());

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-location-privacy-load-failed").TextContent
                .Should()
                .Contain("This catch could not be loaded"));
        await store.Received(1).GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>());
        dialog.Result.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldNotLoadAnotherUsersCatchOrCoordinates()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var store = Substitute.For<ICatchStore>();
        store.GetAllAsync(OtherUserId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CatchModel>>([]));
        store.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CatchModel>>([LocatedCatch(catchId)]));
        var owner = Substitute.For<ILocalCatchOwnerService>();
        owner.GetUserIdAsync(Arg.Any<CancellationToken>()).Returns(OtherUserId);
        var client = Substitute.For<ICatchClient>();
        await using var context = CreateContext(store, client, owner);

        // Act
        var (cut, _) = await ShowModalAsync(context, catchId);

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-location-privacy-load-failed").TextContent
                .Should()
                .Contain("This catch could not be loaded"));
        cut.FindAll("#catch-location-privacy-options").Should().BeEmpty();
        cut.FindAll("#catch-location-privacy-no-location").Should().BeEmpty();
        cut.Markup.Should().NotContain("53.2707");
        cut.Markup.Should().NotContain("-9.0568");
        await owner.Received(1).GetUserIdAsync(Arg.Any<CancellationToken>());
        await store.Received(1).GetAllAsync(OtherUserId, Arg.Any<CancellationToken>());
        await store.DidNotReceive().GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>());
        await client.DidNotReceive().UpdateLocationVisibilityAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowThePublicWarningWhenTheCatchIsAlreadyPublic()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var store = Substitute.For<ICatchStore>();
        store.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CatchModel>>([LocatedCatch(catchId, LocationDefaults.Public)]));
        await using var context = CreateContext(store);

        // Act
        var (cut, dialog) = await ShowModalAsync(context, catchId);

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-location-privacy-public-warning").TextContent
                .Should()
                .Contain("Anyone who can view this catch may see the exact fishing spot"));
        await store.Received(1).GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>());
        dialog.Result.IsCompleted.Should().BeFalse();
    }
}
