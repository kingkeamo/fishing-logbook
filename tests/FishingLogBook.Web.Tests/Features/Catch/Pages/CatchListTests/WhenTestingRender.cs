using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Pages.CatchList;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.CatchListTests;

public class WhenTestingRender : BaseCatchListTest
{
    [Fact]
    public void ItShouldRequireAnAuthenticatedUser()
    {
        // Arrange
        // Act
        var authorize = typeof(CatchList)
            .GetCustomAttributes(inherit: true)
            .OfType<AuthorizeAttribute>()
            .SingleOrDefault();

        // Assert
        authorize.Should().NotBeNull();
    }

    [Fact]
    public async Task ItShouldShowTheFallbackLabelThumbnailAndTime()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var photographId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var stored = new CatchModel(
            catchId,
            DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            [new CatchPhotographModel(photographId, catchId, PhotographContentTypeConstants.Jpeg, [1, 2, 3])]);
        var store = Substitute.For<ICatchStore>();
        store.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CatchModel>>([stored]));
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<CatchList>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find($"#catch-row-{catchId:D}").Should().NotBeNull();
            cut.Find($"#catch-label-{catchId:D}").TextContent.Should().Contain("Catch");
            cut.Find($"#catch-thumb-{catchId:D}").GetAttribute("src").Should().StartWith("data:image/jpeg;base64,");
            cut.Find($"#catch-time-{catchId:D}").TextContent.Should().NotBeNullOrWhiteSpace();
            cut.Find($"#catch-angler-{catchId:D}").TextContent.Should().Contain("Angler: You");
            cut.Find($"#catch-recorded-by-{catchId:D}").TextContent.Should().Contain("Recorded by: You");
            cut.Find($"#catch-edit-{catchId:D}").TextContent.Should().Contain("Edit details");
            cut.Find($"#catch-edit-{catchId:D}").GetAttribute("href").Should().Be($"/catches/{catchId:D}/edit");
        });
        await store.Received(1).GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldUseTheFirstOrderedPhotographAsTheThumbnail()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var firstPhotographId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var stored = new CatchModel(
            catchId,
            DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            [
                new CatchPhotographModel(
                    firstPhotographId,
                    catchId,
                    PhotographContentTypeConstants.Jpeg,
                    [1, 2, 3]),
                new CatchPhotographModel(
                    Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    catchId,
                    PhotographContentTypeConstants.Png,
                    [4, 5, 6]),
                new CatchPhotographModel(
                    Guid.Parse("00000000-0000-0000-0000-000000000003"),
                    catchId,
                    PhotographContentTypeConstants.Webp,
                    [7, 8, 9])
            ]);
        var store = Substitute.For<ICatchStore>();
        store.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CatchModel>>([stored]));
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<CatchList>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find($"#catch-thumb-{catchId:D}").GetAttribute("src")
                .Should()
                .Be($"data:image/jpeg;base64,{Convert.ToBase64String(new byte[] { 1, 2, 3 })}");
        });
        await store.Received(1).GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowTheFrenchFallbackLabel()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        var catchId = Guid.NewGuid();
        var stored = new CatchModel(
            catchId,
            DateTimeOffset.UtcNow,
            [new CatchPhotographModel(Guid.NewGuid(), catchId, PhotographContentTypeConstants.Jpeg, [1])]);
        var store = Substitute.For<ICatchStore>();
        store.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CatchModel>>([stored]));
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<CatchList>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find($"#catch-label-{catchId:D}").TextContent.Should().Contain("Prise");
            cut.Find($"#catch-angler-{catchId:D}").TextContent.Should().Contain("Pêcheur : Vous");
            cut.Find($"#catch-recorded-by-{catchId:D}").TextContent.Should().Contain("Enregistré par : Vous");
            cut.Find($"#catch-edit-{catchId:D}").TextContent.Should().Contain("Modifier les détails");
        });
        cut.FindAll("#catch-list-empty").Should().BeEmpty();
        await store.Received(1).GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowEmptyCopyWhenNoCatchesExist()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CatchModel>>([]));
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<CatchList>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-list-empty").TextContent.Should().Contain("No catches saved"));
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
        var cut = context.Render<CatchList>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-list-load-failed").TextContent.Should().Contain("could not be loaded"));
        await store.Received(1).GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowTheLocationPrivacyLinkWhenTheCatchHasALocation()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var stored = new CatchModel(
            catchId,
            DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            [new CatchPhotographModel(Guid.NewGuid(), catchId, PhotographContentTypeConstants.Jpeg, [1, 2, 3])],
            Location: new CatchLocationModel(
                53.2707,
                -9.0568,
                12,
                DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
                LocationDefaults.DeviceGps,
                LocationDefaults.Private,
                LocationDefaults.ConsentVersion));
        var store = Substitute.For<ICatchStore>();
        store.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CatchModel>>([stored]));
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<CatchList>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            var link = cut.Find($"#catch-location-privacy-{catchId:D}");
            link.TextContent.Should().Contain("Location privacy");
            link.GetAttribute("href").Should().Be($"/catches/{catchId:D}/location-privacy");
        });
        cut.WaitForAssertion(() =>
        {
            var edit = cut.Find($"#catch-edit-{catchId:D}");
            edit.TextContent.Should().Contain("Edit details");
            edit.GetAttribute("href").Should().Be($"/catches/{catchId:D}/edit");
        });
        cut.Markup.Should().NotContain("53.2707");
        cut.Markup.Should().NotContain("-9.0568");
        await store.Received(1).GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldOmitTheLocationPrivacyLinkWhenTheCatchHasNoLocation()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var stored = new CatchModel(
            catchId,
            DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            [new CatchPhotographModel(Guid.NewGuid(), catchId, PhotographContentTypeConstants.Jpeg, [1])]);
        var store = Substitute.For<ICatchStore>();
        store.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CatchModel>>([stored]));
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<CatchList>();

        // Assert
        cut.WaitForAssertion(() => cut.Find($"#catch-row-{catchId:D}").Should().NotBeNull());
        cut.FindAll($"#catch-location-privacy-{catchId:D}").Should().BeEmpty();
        cut.Markup.Should().NotContain("/location-privacy");
        await store.Received(1).GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowTheFrenchLocationPrivacyLink()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var stored = new CatchModel(
            catchId,
            DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            [new CatchPhotographModel(Guid.NewGuid(), catchId, PhotographContentTypeConstants.Jpeg, [1])],
            Location: new CatchLocationModel(
                53.2707,
                -9.0568,
                12,
                DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
                LocationDefaults.DeviceGps,
                LocationDefaults.Private,
                LocationDefaults.ConsentVersion));
        var store = Substitute.For<ICatchStore>();
        store.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CatchModel>>([stored]));
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<CatchList>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#catch-location-privacy-{catchId:D}").TextContent
                .Should()
                .Contain("Confidentialité de la localisation"));
        cut.Markup.Should().NotContain("53.2707");
        await store.Received(1).GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotShowAnotherUsersCatchAfterAccountSwitch()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var ownerCatchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var stored = new CatchModel(
            ownerCatchId,
            DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            [new CatchPhotographModel(Guid.NewGuid(), ownerCatchId, PhotographContentTypeConstants.Jpeg, [1, 2, 3])],
            Location: new CatchLocationModel(
                53.2707,
                -9.0568,
                12,
                DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
                LocationDefaults.DeviceGps,
                LocationDefaults.Private,
                LocationDefaults.ConsentVersion),
            UserId: OwnerUserId);
        var store = Substitute.For<ICatchStore>();
        store.GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CatchModel>>([stored]));
        store.GetAllAsync(OtherUserId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CatchModel>>([]));
        var owner = Substitute.For<ILocalCatchOwnerService>();
        owner.GetUserIdAsync(Arg.Any<CancellationToken>()).Returns(OtherUserId);
        await using var context = CreateContext(store, owner);

        // Act
        var cut = context.Render<CatchList>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-list-empty").TextContent.Should().Contain("No catches saved"));
        cut.FindAll($"#catch-row-{ownerCatchId:D}").Should().BeEmpty();
        cut.Markup.Should().NotContain("53.2707");
        cut.Markup.Should().NotContain("/location-privacy");
        await owner.Received(1).GetUserIdAsync(Arg.Any<CancellationToken>());
        await store.Received(1).GetAllAsync(OtherUserId, Arg.Any<CancellationToken>());
        await store.DidNotReceive().GetAllAsync(OwnerUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowLoadFailedCopyWhenTheOwnerCannotBeResolved()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        var owner = Substitute.For<ILocalCatchOwnerService>();
        owner.GetUserIdAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("The current user is not signed in."));
        await using var context = CreateContext(store, owner);

        // Act
        var cut = context.Render<CatchList>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-list-load-failed").TextContent.Should().Contain("could not be loaded"));
        await owner.Received(1).GetUserIdAsync(Arg.Any<CancellationToken>());
        await store.DidNotReceive().GetAllAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
