using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Pages.CatchEdit;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;

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
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(p => p.CatchId, catchId));

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#catch-edit-title").TextContent.Should().Contain("Edit catch");
            cut.Find("#catch-edit-species").Should().NotBeNull();
            cut.Find("#catch-edit-weight").Should().NotBeNull();
            cut.Find("#catch-edit-length").Should().NotBeNull();
            cut.Find("#catch-edit-method").Should().NotBeNull();
            cut.Find("#catch-edit-bait").Should().NotBeNull();
            cut.Find("#catch-edit-notes").Should().NotBeNull();
            cut.Find("#catch-edit-caught-on").Should().NotBeNull();
            cut.Find("#catch-edit-save").TextContent.Should().Contain("Save details");
        });
        await store.Received(1).GetAsync(OwnerUserId, catchId, Arg.Any<CancellationToken>());
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
}
