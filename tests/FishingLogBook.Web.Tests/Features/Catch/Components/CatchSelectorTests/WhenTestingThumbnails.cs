using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Features.Catch.Components.CatchSelector;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Components.CatchSelectorTests;

public class WhenTestingThumbnails : BaseCatchSelectorTest
{
    [Fact]
    public async Task ItShouldShowAPlaceholderWhenACatchHasNoPhotograph()
    {
        // Arrange
        await using var context = CreateContext();
        var withoutPhoto = new CatchModel(PikeCatchId, CaughtOn, [], "Pike", UserId: OwnerUserId);

        // Act
        var cut = context.Render<CatchSelector>(parameters => parameters
            .Add(component => component.Catches, new[] { withoutPhoto })
            .Add(component => component.OwnerUserId, OwnerUserId)
            .Add(component => component.ConfirmLabel, "Add to this trip")
            .Add(component => component.EmptyLabel, "Nothing to add"));

        // Assert
        cut.Find(".catch-selector-thumbnail-placeholder").Should().NotBeNull();
        cut.FindAll(".catch-selector-thumbnail img").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldRenderARemotePhotographWithoutReadingLocalBytes()
    {
        // Arrange
        var photographId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var catchStore = Substitute.For<ICatchStore>();
        await using var context = CreateContext(catchStore);
        var withRemotePhoto = new CatchModel(
            PikeCatchId,
            CaughtOn,
            [
                new CatchPhotographModel(
                    photographId,
                    PikeCatchId,
                    PhotographContentTypeConstants.Jpeg,
                    RemoteUrl: "https://storage.test/pike.jpg?signed=1")
            ],
            "Pike",
            UserId: OwnerUserId);

        // Act
        var cut = context.Render<CatchSelector>(parameters => parameters
            .Add(component => component.Catches, new[] { withRemotePhoto })
            .Add(component => component.OwnerUserId, OwnerUserId)
            .Add(component => component.ConfirmLabel, "Add to this trip")
            .Add(component => component.EmptyLabel, "Nothing to add"));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find(".catch-selector-thumbnail").GetAttribute("src")
                .Should().Be("https://storage.test/pike.jpg?signed=1"));
        await catchStore.DidNotReceive().GetPhotographBytesAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldLoadALocalThumbnailFromTheCatchStore()
    {
        // Arrange
        var photographId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var catchStore = CatchStoreWithPhotographBytes(photographId, [9, 9, 9]);
        await using var context = CreateContext(catchStore);
        var withLocalPhoto = new CatchModel(
            PikeCatchId,
            CaughtOn,
            [new CatchPhotographModel(photographId, PikeCatchId, PhotographContentTypeConstants.Jpeg)],
            "Pike",
            UserId: OwnerUserId);

        // Act
        var cut = context.Render<CatchSelector>(parameters => parameters
            .Add(component => component.Catches, new[] { withLocalPhoto })
            .Add(component => component.OwnerUserId, OwnerUserId)
            .Add(component => component.ConfirmLabel, "Add to this trip")
            .Add(component => component.EmptyLabel, "Nothing to add"));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find(".catch-selector-thumbnail").GetAttribute("src")
                .Should().StartWith("data:image/jpeg;base64,"));
        await catchStore.Received(1).GetPhotographBytesAsync(
            OwnerUserId,
            PikeCatchId,
            photographId,
            Arg.Any<CancellationToken>());
    }
}
