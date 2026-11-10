using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline.Stores;
using FishingLogBook.Web.Features.Catch.Pages.CatchEdit;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Catch.Pages.CatchEditTests;

public class WhenTestingLocationVisibilityLabels : BaseCatchEditTest
{
    private static readonly Guid CatchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Theory]
    [InlineData(LocationDefaults.Private, "Catch_LocationVisibilityPrivate")]
    [InlineData(LocationDefaults.Approximate, "Catch_LocationVisibilityApproximate")]
    [InlineData(LocationDefaults.FishingVenueOnly, "Catch_LocationVisibilityFishingVenueOnly")]
    [InlineData(LocationDefaults.Public, "Catch_LocationVisibilityPublic")]
    public async Task ItShouldDescribeEachLocationVisibility(string visibility, string resourceKey)
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var location = new CatchLocationModel(
            53.2707,
            -9.0568,
            12,
            DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            LocationDefaults.DeviceGps,
            visibility,
            LocationDefaults.ConsentVersion);
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, CatchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(CatchId, location: location));
        await using var context = CreateContext(store);
        var expected = context.Services.GetRequiredService<IStringLocalizer<UiStrings>>()[resourceKey].Value;

        // Act
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(page => page.CatchId, CatchId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-edit-location-visibility").TextContent.Should().Contain(expected));
        await store.Received().GetAsync(OwnerUserId, CatchId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowNoVisibilityDescriptionForAnUnrecognisedValue()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var location = new CatchLocationModel(
            53.2707,
            -9.0568,
            12,
            DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
            LocationDefaults.DeviceGps,
            "Everyone",
            LocationDefaults.ConsentVersion);
        var store = Substitute.For<ICatchStore>();
        store.GetAsync(OwnerUserId, CatchId, Arg.Any<CancellationToken>())
            .Returns(StoredCatch(CatchId, location: location));
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<CatchEdit>(parameters => parameters.Add(page => page.CatchId, CatchId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#catch-edit-location-visibility").Should().NotBeNull());
        cut.Find("#catch-edit-location-visibility").TextContent.Trim().Should().BeEmpty();
    }
}
