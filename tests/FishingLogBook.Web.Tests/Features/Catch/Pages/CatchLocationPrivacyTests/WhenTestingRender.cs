using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Pages.CatchLocationPrivacy;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.CatchLocationPrivacyTests;

public class WhenTestingRender : BaseCatchLocationPrivacyTest
{
    [Fact]
    public void ItShouldRequireAnAuthenticatedUser()
    {
        // Arrange
        // Act
        var authorize = typeof(CatchLocationPrivacy)
            .GetCustomAttributes(inherit: true)
            .OfType<AuthorizeAttribute>()
            .SingleOrDefault();

        // Assert
        authorize.Should().NotBeNull();
    }

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
        var cut = context.Render<CatchLocationPrivacy>(parameters => parameters.Add(p => p.CatchId, catchId));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-location-privacy-no-location").TextContent
                .Should()
                .Contain("This catch has no saved location"));
        cut.FindAll("#catch-location-privacy-options").Should().BeEmpty();
        await store.Received(1).GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>());
        await client.DidNotReceive().UpdateLocationVisibilityAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
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
        var cut = context.Render<CatchLocationPrivacy>(parameters => parameters.Add(p => p.CatchId, catchId));

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
            options.Should().Contain("Approximate area");
            options.Should().Contain("Fishing venue only");
            options.Should().Contain("Public exact location");
        });
        cut.FindAll("#catch-location-privacy-public-warning").Should().BeEmpty();
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
        var cut = context.Render<CatchLocationPrivacy>(parameters => parameters.Add(p => p.CatchId, catchId));

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#catch-location-privacy-title").TextContent
                .Should()
                .Contain("Confidentialité de la localisation");
            var options = cut.Find("#catch-location-privacy-options").TextContent;
            options.Should().Contain("Moi uniquement");
            options.Should().Contain("Zone approximative");
            options.Should().Contain("Site de pêche uniquement");
            options.Should().Contain("Position exacte publique");
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
        var cut = context.Render<CatchLocationPrivacy>(parameters =>
            parameters.Add(p => p.CatchId, Guid.NewGuid()));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-location-privacy-load-failed").TextContent
                .Should()
                .Contain("This catch could not be loaded"));
        await store.Received(1).GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>());
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
        var cut = context.Render<CatchLocationPrivacy>(parameters => parameters.Add(p => p.CatchId, catchId));

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
}
