using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Pages.CatchList;
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
        store.GetAllAsync(Arg.Any<CancellationToken>())
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
        });
        await store.Received(1).GetAllAsync(Arg.Any<CancellationToken>());
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
        store.GetAllAsync(Arg.Any<CancellationToken>())
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
        await store.Received(1).GetAllAsync(Arg.Any<CancellationToken>());
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
        store.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CatchModel>>([stored]));
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<CatchList>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find($"#catch-label-{catchId:D}").TextContent.Should().Contain("Prise"));
        cut.FindAll("#catch-list-empty").Should().BeEmpty();
        await store.Received(1).GetAllAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowEmptyCopyWhenNoCatchesExist()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<CatchModel>>([]));
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<CatchList>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-list-empty").TextContent.Should().Contain("No catches saved"));
        await store.Received(1).GetAllAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowLoadFailedCopyWhenTheStoreFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = Substitute.For<ICatchStore>();
        store.GetAllAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("IndexedDB failed."));
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<CatchList>();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#catch-list-load-failed").TextContent.Should().Contain("could not be loaded"));
        await store.Received(1).GetAllAsync(Arg.Any<CancellationToken>());
    }
}
