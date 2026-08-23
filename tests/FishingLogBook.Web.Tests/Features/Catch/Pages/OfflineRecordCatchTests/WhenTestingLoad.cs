using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Pages.OfflineRecordCatch;
using FishingLogBook.Web.Features.Profile.Offline.Stores;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.OfflineRecordCatchTests;

public class WhenTestingLoad : BaseOfflineRecordCatchTest
{
    [Fact]
    public async Task ItShouldUseOnlyTheUnlockedOwnerAndCachedPreferences()
    {
        // Arrange
        var catchStore = Substitute.For<ICatchStore>();
        var preferencesStore = Substitute.For<IAnglerPreferencesStore>();
        preferencesStore.GetAsync(OwnerUserId, Arg.Any<CancellationToken>()).Returns(Preferences());
        await using var context = CreateContext(catchStore, preferencesStore);

        // Act
        var cut = context.Render<OfflineRecordCatch>();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#record-catch-method-Fly").Should().NotBeNull());
        cut.Find("#record-catch-species-BrownTrout").Should().NotBeNull();
        await preferencesStore.Received(1).GetAsync(OwnerUserId, Arg.Any<CancellationToken>());
        await catchStore.DidNotReceive().SaveAsync(Arg.Any<FishingLogBook.Web.Features.Catch.Models.CatchModel>(), Arg.Any<CancellationToken>());
    }
}
