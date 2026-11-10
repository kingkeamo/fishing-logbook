using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Pages.CatchEdit;
using FishingLogBook.Web.Localization;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.CatchEditTests;

public class WhenTestingSaveValidation : BaseCatchEditTest
{
    private static readonly Guid CatchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private async Task<(IRenderedComponent<CatchEdit> Cut, ICatchStore Store, Web.Common.Offline.Synchronisers.ILogbookSynchroniser Synchroniser)>
        RenderAsync(BunitContextHolder holder, CatchModel stored)
    {
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, CatchId, Arg.Any<CancellationToken>()).Returns(stored);
        var synchroniser = QuietSynchroniser();
        holder.Context = CreateContext(store, synchroniser: synchroniser);
        var cut = holder.Context.Render<CatchEdit>(parameters => parameters.Add(p => p.CatchId, CatchId));
        cut.WaitForAssertion(() => cut.Find("#catch-edit-save").Should().NotBeNull());
        await Task.CompletedTask;
        return (cut, store, synchroniser);
    }

    [Fact]
    public async Task ItShouldRejectANonPositiveLengthWithoutSaving()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var holder = new BunitContextHolder();
        var (cut, store, synchroniser) = await RenderAsync(
            holder,
            StoredCatch(CatchId, SyncStatus.Synchronised, SyncStatus.Synchronised, length: 0m));

        // Act
        await cut.Find("#catch-edit-save").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-edit-validation").TextContent
            .Should().Contain("Enter a measurement greater than zero"));
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
        await synchroniser.DidNotReceive().SynchronisePendingAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("#catch-edit-bait", CatchDetailConstants.MaxBaitOrLureLength)]
    [InlineData("#catch-edit-notes", CatchDetailConstants.MaxNotesLength)]
    public async Task ItShouldRejectTextOneCharacterOverItsLimitWithoutSaving(string selector, int limit)
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var holder = new BunitContextHolder();
        var (cut, store, synchroniser) = await RenderAsync(
            holder,
            StoredCatch(CatchId, SyncStatus.Synchronised, SyncStatus.Synchronised));
        cut.Find(selector).Input(new string('x', limit + 1));

        // Act
        await cut.Find("#catch-edit-save").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-edit-validation").Should().NotBeNull());
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
        await synchroniser.DidNotReceive().SynchronisePendingAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("#catch-edit-bait", CatchDetailConstants.MaxBaitOrLureLength)]
    [InlineData("#catch-edit-notes", CatchDetailConstants.MaxNotesLength)]
    public async Task ItShouldAcceptTextExactlyAtItsLimit(string selector, int limit)
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var holder = new BunitContextHolder();
        var (cut, store, _) = await RenderAsync(
            holder,
            StoredCatch(CatchId, SyncStatus.Synchronised, SyncStatus.Synchronised));
        cut.Find(selector).Input(new string('x', limit));

        // Act
        await cut.Find("#catch-edit-save").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-edit-saved").Should().NotBeNull());
        cut.FindAll("#catch-edit-validation").Should().BeEmpty();
        await store.Received(1).SaveAsync(
            Arg.Is<CatchModel>(saved => saved.Id == CatchId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRejectACaughtOnThatTheBrowserCannotConvert()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var holder = new BunitContextHolder();
        var (cut, store, synchroniser) = await RenderAsync(
            holder,
            StoredCatch(CatchId, SyncStatus.Synchronised, SyncStatus.Synchronised));
        cut.Find("#catch-edit-caught-on").Input("not a date");

        // Act
        await cut.Find("#catch-edit-save").ClickAsync();

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-edit-validation").Should().NotBeNull());
        await store.DidNotReceive().SaveAsync(Arg.Any<CatchModel>(), Arg.Any<CancellationToken>());
        await synchroniser.DidNotReceive().SynchronisePendingAsync(Arg.Any<CancellationToken>());
    }

    private sealed class BunitContextHolder : IAsyncDisposable
    {
        public BunitContext? Context { get; set; }

        public async ValueTask DisposeAsync()
        {
            if (Context is not null)
            {
                await Context.DisposeAsync();
            }
        }
    }
}
