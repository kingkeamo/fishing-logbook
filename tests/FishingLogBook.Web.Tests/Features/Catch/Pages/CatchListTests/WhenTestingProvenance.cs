using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Pages.CatchList;
using FishingLogBook.Web.Localization;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.CatchListTests;

public class WhenTestingProvenance : BaseCatchListTest
{
    [Fact]
    public async Task ItShouldHideProvenanceWhenTheAnglerAndRecorderAreTheCurrentUser()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.NewGuid();
        var store = Substitute.For<ICatchStore>();
        store.GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<CatchModel>>(
            [
                StoredCatch(
                    catchId,
                    DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
                    anglerUserId: OwnerUserId,
                    recordedByUserId: OwnerUserId)
            ]);
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<CatchList>();

        // Assert
        cut.WaitForAssertion(() => cut.Find($"#catch-card-{catchId:D}").Should().NotBeNull());
        cut.FindAll($"#catch-card-provenance-{catchId:D}").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldShowProvenanceWhenTheAnglerIsSomeoneElse()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.NewGuid();
        var store = Substitute.For<ICatchStore>();
        store.GetMetadataAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<CatchModel>>(
            [
                StoredCatch(
                    catchId,
                    DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
                    anglerUserId: OtherUserId,
                    recordedByUserId: OwnerUserId)
            ]);
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<CatchList>();

        // Assert
        cut.WaitForAssertion(() =>
        {
            var provenance = cut.Find($"#catch-card-provenance-{catchId:D}").TextContent;
            provenance.Should().NotBeNullOrWhiteSpace();
            provenance.Should().NotContain(OtherUserId.ToString());
        });
    }
}
