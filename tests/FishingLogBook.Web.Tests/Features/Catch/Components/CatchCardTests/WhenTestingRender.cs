using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Enums;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Catch.Components.CatchCard;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Localization;

namespace FishingLogBook.Web.Tests.Features.Catch.Components.CatchCardTests;

public class WhenTestingRender : BaseCatchCardTest
{
    [Fact]
    public async Task ItShouldPositionTheActionsMenuRelativeToTheCard()
    {
        // Arrange
        var catchId = Guid.NewGuid();
        await using var context = CreateContext();

        // Act
        var cut = context.Render<CatchCard>(parameters => parameters
            .Add(component => component.Catch, StoredCatch(catchId)));

        // Assert
        cut.Find(".catch-card-positioner").Should().NotBeNull();
        cut.Find($"#catch-card-menu-{catchId:D}").Should().NotBeNull();
    }

    [Fact]
    public async Task ItShouldRenderThePhotographThumbnail()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.NewGuid();
        var stored = StoredCatch(catchId);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<CatchCard>(parameters => parameters
            .Add(card => card.Catch, stored)
            .Add(card => card.LocalCaughtOn, LocalToday.AddHours(16).AddMinutes(50))
            .Add(card => card.LocalToday, LocalToday));

        // Assert
        cut.Find($"#catch-card-photo-{catchId:D}").GetAttribute("src")
            .Should().StartWith($"data:{PhotographContentTypeConstants.Jpeg};base64,");
        cut.FindAll($"#catch-card-no-photo-{catchId:D}").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldRenderAPlaceholderWhenThereIsNoPhotograph()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.NewGuid();
        var stored = StoredCatch(catchId, withPhotograph: false);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<CatchCard>(parameters => parameters
            .Add(card => card.Catch, stored)
            .Add(card => card.LocalCaughtOn, LocalToday)
            .Add(card => card.LocalToday, LocalToday));

        // Assert
        cut.FindAll($"#catch-card-photo-{catchId:D}").Should().BeEmpty();
        var placeholder = cut.Find($"#catch-card-no-photo-{catchId:D}");
        placeholder.GetAttribute("aria-label").Should().Be("No photograph");
    }

    [Fact]
    public async Task ItShouldRenderARemoteUrlPhotographWhenNoLocalBytesArePresent()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.NewGuid();
        var stored = new CatchModel(
            catchId,
            DateTimeOffset.Parse("2026-08-17T16:50:00Z"),
            [new CatchPhotographModel(
                Guid.NewGuid(),
                catchId,
                "image/jpeg",
                RemoteUrl: "https://r2.test/signed-download")],
            SpeciesName: "Brown Trout",
            UserId: OwnerUserId,
            AnglerUserId: OwnerUserId,
            RecordedByUserId: OwnerUserId);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<CatchCard>(parameters => parameters
            .Add(card => card.Catch, stored)
            .Add(card => card.LocalCaughtOn, LocalToday)
            .Add(card => card.LocalToday, LocalToday));

        // Assert
        cut.Find($"#catch-card-photo-{catchId:D}").GetAttribute("src")
            .Should().Be("https://r2.test/signed-download");
    }

    [Fact]
    public async Task ItShouldShowTheSpeciesNameAsThePrimaryLabel()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.NewGuid();
        var stored = StoredCatch(catchId, speciesName: "Pike");
        await using var context = CreateContext();

        // Act
        var cut = context.Render<CatchCard>(parameters => parameters
            .Add(card => card.Catch, stored)
            .Add(card => card.LocalCaughtOn, LocalToday)
            .Add(card => card.LocalToday, LocalToday));

        // Assert
        cut.Find($"#catch-card-species-{catchId:D}").TextContent.Should().Contain("Pike");
    }

    [Fact]
    public async Task ItShouldFallBackToTheUnknownSpeciesLabelWhenSpeciesIsMissing()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.NewGuid();
        var stored = StoredCatch(catchId, speciesName: null);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<CatchCard>(parameters => parameters
            .Add(card => card.Catch, stored)
            .Add(card => card.LocalCaughtOn, LocalToday)
            .Add(card => card.LocalToday, LocalToday));

        // Assert
        cut.Find($"#catch-card-species-{catchId:D}").TextContent.Should().Contain("Catch");
    }

    [Fact]
    public async Task ItShouldShowTodayAndTheLocalTime()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.NewGuid();
        var stored = StoredCatch(catchId);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<CatchCard>(parameters => parameters
            .Add(card => card.Catch, stored)
            .Add(card => card.LocalCaughtOn, LocalToday.AddHours(16).AddMinutes(50))
            .Add(card => card.LocalToday, LocalToday));

        // Assert
        var text = cut.Find($"#catch-card-time-{catchId:D}").TextContent;
        text.Should().Contain("Today");
        text.Should().Contain("16:50");
    }

    [Fact]
    public async Task ItShouldShowTheMethodWhenPopulated()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.NewGuid();
        var stored = StoredCatch(catchId, method: "Fly");
        await using var context = CreateContext();

        // Act
        var cut = context.Render<CatchCard>(parameters => parameters
            .Add(card => card.Catch, stored)
            .Add(card => card.LocalCaughtOn, LocalToday)
            .Add(card => card.LocalToday, LocalToday));

        // Assert
        cut.Find($"#catch-card-method-{catchId:D}").TextContent.Should().Contain("Fly");
    }

    [Fact]
    public async Task ItShouldOmitTheMethodRowWhenNotPopulated()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.NewGuid();
        var stored = StoredCatch(catchId, method: null);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<CatchCard>(parameters => parameters
            .Add(card => card.Catch, stored)
            .Add(card => card.LocalCaughtOn, LocalToday)
            .Add(card => card.LocalToday, LocalToday));

        // Assert
        cut.FindAll($"#catch-card-method-{catchId:D}").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldShowWeightAndLengthWhenBothArePresent()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.NewGuid();
        var stored = StoredCatch(catchId, weight: 2.5m, length: 43m);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<CatchCard>(parameters => parameters
            .Add(card => card.Catch, stored)
            .Add(card => card.LocalCaughtOn, LocalToday)
            .Add(card => card.LocalToday, LocalToday)
            .Add(card => card.WeightUnit, WeightUnitEnum.Kg)
            .Add(card => card.LengthUnit, LengthUnitEnum.Cm));

        // Assert
        var text = cut.Find($"#catch-card-measurements-{catchId:D}").TextContent;
        text.Should().Contain("2.5 kg");
        text.Should().Contain("43 cm");
    }

    [Fact]
    public async Task ItShouldOmitTheMeasurementsRowWhenNeitherIsPresent()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.NewGuid();
        var stored = StoredCatch(catchId, weight: null, length: null);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<CatchCard>(parameters => parameters
            .Add(card => card.Catch, stored)
            .Add(card => card.LocalCaughtOn, LocalToday)
            .Add(card => card.LocalToday, LocalToday));

        // Assert
        cut.FindAll($"#catch-card-measurements-{catchId:D}").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldShowBaitOrLureWhenPopulated()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.NewGuid();
        var stored = StoredCatch(catchId, baitOrLure: "Woolly Bugger");
        await using var context = CreateContext();

        // Act
        var cut = context.Render<CatchCard>(parameters => parameters
            .Add(card => card.Catch, stored)
            .Add(card => card.LocalCaughtOn, LocalToday)
            .Add(card => card.LocalToday, LocalToday));

        // Assert
        cut.Find($"#catch-card-bait-{catchId:D}").TextContent.Should().Contain("Woolly Bugger");
    }

    [Fact]
    public async Task ItShouldOmitTheBaitOrLureRowWhenNotPopulated()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.NewGuid();
        var stored = StoredCatch(catchId, baitOrLure: null);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<CatchCard>(parameters => parameters
            .Add(card => card.Catch, stored)
            .Add(card => card.LocalCaughtOn, LocalToday)
            .Add(card => card.LocalToday, LocalToday));

        // Assert
        cut.FindAll($"#catch-card-bait-{catchId:D}").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldShowANotesPreviewWhenPopulated()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.NewGuid();
        var stored = StoredCatch(catchId, notes: "Took a slow retrieve near the reeds to trigger a take.");
        await using var context = CreateContext();

        // Act
        var cut = context.Render<CatchCard>(parameters => parameters
            .Add(card => card.Catch, stored)
            .Add(card => card.LocalCaughtOn, LocalToday)
            .Add(card => card.LocalToday, LocalToday));

        // Assert
        cut.Find($"#catch-card-notes-{catchId:D}").TextContent.Should().Contain("Took a slow retrieve");
    }

    [Fact]
    public async Task ItShouldOmitTheNotesRowWhenNotPopulated()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.NewGuid();
        var stored = StoredCatch(catchId, notes: null);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<CatchCard>(parameters => parameters
            .Add(card => card.Catch, stored)
            .Add(card => card.LocalCaughtOn, LocalToday)
            .Add(card => card.LocalToday, LocalToday));

        // Assert
        cut.FindAll($"#catch-card-notes-{catchId:D}").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldRenderASingleSimplePhotographWithoutCarouselControls()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.NewGuid();
        var stored = StoredCatch(catchId, photographCount: 1);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<CatchCard>(parameters => parameters
            .Add(card => card.Catch, stored)
            .Add(card => card.LocalCaughtOn, LocalToday)
            .Add(card => card.LocalToday, LocalToday));

        // Assert
        cut.Find($"#catch-card-photo-{catchId:D}").Should().NotBeNull();
        cut.FindAll($"#catch-card-carousel-{catchId:D}").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldMakeAllPhotographsReachableWhenThereAreMultiple()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.NewGuid();
        var stored = StoredCatch(catchId, photographCount: 3);

        await using var context = CreateContext();

        var cut = context.Render<CatchCard>(parameters => parameters
            .Add(card => card.Catch, stored)
            .Add(card => card.LocalCaughtOn, LocalToday)
            .Add(card => card.LocalToday, LocalToday));

        var firstSrc = cut
            .Find($"#catch-card-photo-{catchId:D}-0")
            .GetAttribute("src");

        // Act
        await cut.Find($"#catch-card-photo-next-{catchId:D}").ClickAsync();

        var secondSrc = cut
            .Find($"#catch-card-photo-{catchId:D}-1")
            .GetAttribute("src");

        await cut.Find($"#catch-card-photo-next-{catchId:D}").ClickAsync();

        var thirdSrc = cut
            .Find($"#catch-card-photo-{catchId:D}-2")
            .GetAttribute("src");

        // Assert
        firstSrc.Should().NotBe(secondSrc);
        secondSrc.Should().NotBe(thirdSrc);

        cut.Find($"#catch-card-photo-count-{catchId:D}")
            .TextContent
            .Should()
            .Contain("3 of 3");

        cut.Find($"#catch-card-photo-{catchId:D}-2")
            .GetAttribute("alt")
            .Should()
            .Contain("3 of 3");

        // Act - wrap back to the first photograph
        await cut.Find($"#catch-card-photo-next-{catchId:D}").ClickAsync();

        // Assert
        cut.Find($"#catch-card-photo-count-{catchId:D}")
            .TextContent
            .Should()
            .Contain("1 of 3");

        cut.Find($"#catch-card-photo-{catchId:D}-0")
            .Should()
            .NotBeNull();
    }

    [Fact]
    public async Task ItShouldRenderRemoteUrlPhotographsInTheCarouselWhenNoLocalBytesArePresent()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.NewGuid();
        var stored = new CatchModel(
            catchId,
            DateTimeOffset.Parse("2026-08-17T16:50:00Z"),
            [
                new CatchPhotographModel(
                    Guid.NewGuid(), catchId, "image/jpeg", RemoteUrl: "https://r2.test/one"),
                new CatchPhotographModel(
                    Guid.NewGuid(), catchId, "image/jpeg", RemoteUrl: "https://r2.test/two")
            ],
            SpeciesName: "Brown Trout",
            UserId: OwnerUserId,
            AnglerUserId: OwnerUserId,
            RecordedByUserId: OwnerUserId);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<CatchCard>(parameters => parameters
            .Add(card => card.Catch, stored)
            .Add(card => card.LocalCaughtOn, LocalToday)
            .Add(card => card.LocalToday, LocalToday));

        // Assert
        cut.Find($"#catch-card-photo-{catchId:D}-0").GetAttribute("src").Should().Be("https://r2.test/one");
    }

    [Fact]
    public async Task ItShouldHideProvenanceWhenTheAnglerAndRecorderAreTheCurrentUser()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.NewGuid();
        var stored = StoredCatch(catchId, anglerUserId: OwnerUserId, recordedByUserId: OwnerUserId);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<CatchCard>(parameters => parameters
            .Add(card => card.Catch, stored)
            .Add(card => card.LocalCaughtOn, LocalToday)
            .Add(card => card.LocalToday, LocalToday)
            .Add(card => card.CurrentUserId, OwnerUserId));

        // Assert
        cut.FindAll($"#catch-card-provenance-{catchId:D}").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldShowProvenanceWhenTheAnglerIsSomeoneElse()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.NewGuid();
        var stored = StoredCatch(catchId, anglerUserId: OtherUserId, recordedByUserId: OwnerUserId);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<CatchCard>(parameters => parameters
            .Add(card => card.Catch, stored)
            .Add(card => card.LocalCaughtOn, LocalToday)
            .Add(card => card.LocalToday, LocalToday)
            .Add(card => card.CurrentUserId, OwnerUserId));

        // Assert
        var provenance = cut.Find($"#catch-card-provenance-{catchId:D}").TextContent;
        provenance.Should().NotBeNullOrWhiteSpace();
        provenance.Should().NotContain(OtherUserId.ToString());
    }

    [Fact]
    public async Task ItShouldShowProvenanceWhenTheRecorderIsSomeoneElse()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.NewGuid();
        var stored = StoredCatch(catchId, anglerUserId: OwnerUserId, recordedByUserId: OtherUserId);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<CatchCard>(parameters => parameters
            .Add(card => card.Catch, stored)
            .Add(card => card.LocalCaughtOn, LocalToday)
            .Add(card => card.LocalToday, LocalToday)
            .Add(card => card.CurrentUserId, OwnerUserId));

        // Assert
        cut.Find($"#catch-card-provenance-{catchId:D}").TextContent.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ItShouldBeQuietWhenTheCatchIsFullySynchronised()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.NewGuid();
        var stored = StoredCatch(catchId, syncStatus: SyncStatus.Synchronised);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<CatchCard>(parameters => parameters
            .Add(card => card.Catch, stored)
            .Add(card => card.LocalCaughtOn, LocalToday)
            .Add(card => card.LocalToday, LocalToday));

        // Assert
        cut.FindAll($"#catch-card-attention-{catchId:D}").Should().BeEmpty();
        cut.FindAll($"#catch-card-synchronising-{catchId:D}").Should().BeEmpty();
        cut.Markup.Should().NotContain("Synchronised");
    }

    [Fact]
    public async Task ItShouldShowAQuietIndicatorWithoutRetryWhileSynchronising()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.NewGuid();
        var stored = StoredCatch(catchId, syncStatus: SyncStatus.Synchronising);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<CatchCard>(parameters => parameters
            .Add(card => card.Catch, stored)
            .Add(card => card.LocalCaughtOn, LocalToday)
            .Add(card => card.LocalToday, LocalToday));

        // Assert
        cut.Find($"#catch-card-synchronising-{catchId:D}").TextContent.Should().Contain("Synchronising");
        cut.FindAll($"#catch-sync-retry-{catchId:D}").Should().BeEmpty();
    }

    [Theory]
    [InlineData(SyncStatus.SavedLocally)]
    [InlineData(SyncStatus.WaitingToSynchronise)]
    public async Task ItShouldShowPendingReassuranceAndRetry(SyncStatus status)
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.NewGuid();
        var stored = StoredCatch(catchId, syncStatus: status);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<CatchCard>(parameters => parameters
            .Add(card => card.Catch, stored)
            .Add(card => card.LocalCaughtOn, LocalToday)
            .Add(card => card.LocalToday, LocalToday));

        // Assert
        cut.Find($"#catch-sync-reassurance-{catchId:D}").TextContent
            .Should().Contain("Saved on this device. It will sync automatically.");
        cut.Find($"#catch-sync-retry-{catchId:D}").Should().NotBeNull();
    }

    [Fact]
    public async Task ItShouldShowFailureReassuranceAndRetry()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.NewGuid();
        var stored = StoredCatch(catchId, syncStatus: SyncStatus.FailedToSynchronise);
        await using var context = CreateContext();

        // Act
        var cut = context.Render<CatchCard>(parameters => parameters
            .Add(card => card.Catch, stored)
            .Add(card => card.LocalCaughtOn, LocalToday)
            .Add(card => card.LocalToday, LocalToday));

        // Assert
        cut.Find($"#catch-sync-reassurance-{catchId:D}").TextContent
            .Should().Contain("Your catch is still saved on this device.");
        cut.Find($"#catch-sync-retry-{catchId:D}").TextContent.Should().Contain("Retry");
    }

    [Fact]
    public async Task ItShouldOmitTheLocationPrivacyMenuItemWhenThereIsNoLocation()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.NewGuid();
        var stored = StoredCatch(catchId, location: null);
        await using var context = CreateContext();
        var (cut, popover) = RenderCard(context, parameters => parameters
            .Add(card => card.Catch, stored)
            .Add(card => card.LocalCaughtOn, LocalToday)
            .Add(card => card.LocalToday, LocalToday));

        // Act
        await cut.Find($"#catch-card-menu-{catchId:D}").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            popover.Find($"#catch-card-edit-{catchId:D}").Should().NotBeNull());
        popover.FindAll($"#catch-card-location-privacy-{catchId:D}").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldShowTheLocationPrivacyMenuItemWhenLocationExists()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var catchId = Guid.NewGuid();
        var stored = StoredCatch(
            catchId,
            location: new CatchLocationModel(
                53.2707,
                -9.0568,
                12,
                DateTimeOffset.Parse("2026-08-17T08:00:00Z"),
                "DeviceGps",
                "Private",
                "1"));
        await using var context = CreateContext();
        var (cut, popover) = RenderCard(context, parameters => parameters
            .Add(card => card.Catch, stored)
            .Add(card => card.LocalCaughtOn, LocalToday)
            .Add(card => card.LocalToday, LocalToday));

        // Act
        await cut.Find($"#catch-card-menu-{catchId:D}").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            popover.Find($"#catch-card-location-privacy-{catchId:D}").TextContent
                .Should().Contain("Location privacy"));
    }
}
