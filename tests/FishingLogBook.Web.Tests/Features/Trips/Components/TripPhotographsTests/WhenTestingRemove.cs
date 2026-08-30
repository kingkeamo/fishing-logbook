using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Photographs.Enums;
using FishingLogBook.Web.Features.Trips.Clients;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.Features.Trips.Offline.Stores.TripPhotographStoreTests;
using Microsoft.AspNetCore.Components.Forms;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TripPhotographsComponent =
    FishingLogBook.Web.Features.Trips.Components.TripPhotographs.TripPhotographs;

namespace FishingLogBook.Web.Tests.Features.Trips.Components.TripPhotographsTests;

public class WhenTestingRemove : BaseTripPhotographsTest
{
    private static readonly Guid FirstPhotographId =
        Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid SecondPhotographId =
        Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

    [Fact]
    public async Task ItShouldWarnAndKeepASynchronisedPhotographWhenTheServerRefuses()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = new MemoryTripPhotographStore();
        var stored = StoredPhotograph(FirstPhotographId, syncStatus: SyncStatus.Synchronised);
        await store.SaveAsync(stored with { Bytes = [1, 2, 3] }, CancellationToken.None);
        var client = Substitute.For<ITripClient>();
        client.DeletePhotographAsync(TripId, FirstPhotographId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Offline."));
        await using var context = CreateContext(store, tripClient: client);
        var cut = context.Render<TripPhotographsComponent>(parameters =>
            parameters.Add(component => component.Trip, Trip(stored))
                .Add(component => component.ViewerUserId, OwnerUserId));
        cut.WaitForAssertion(() => cut.Find("#trip-photo-remove").Should().NotBeNull());

        // Act
        await cut.Find("#trip-photo-remove").ClickAsync();

        // Assert
        cut.Find("#trip-photo-remove-failed").TextContent.Should().Contain("could not be removed");
        store.Count.Should().Be(1);
        store.Stored(FirstPhotographId).Should().NotBeNull();
    }

    [Fact]
    public async Task ItShouldRemoveALocalPhotographWithoutContactingTheServer()
    {
        // Arrange
        var store = new MemoryTripPhotographStore();
        var stored = StoredPhotograph(FirstPhotographId);
        await store.SaveAsync(stored with { Bytes = [1, 2, 3] }, CancellationToken.None);
        var client = Substitute.For<ITripClient>();
        await using var context = CreateContext(store, tripClient: client);
        var cut = context.Render<TripPhotographsComponent>(parameters =>
            parameters.Add(component => component.Trip, Trip(stored))
                .Add(component => component.ViewerUserId, OwnerUserId));
        cut.WaitForAssertion(() => cut.Find("#trip-photo-remove").Should().NotBeNull());

        // Act
        await cut.Find("#trip-photo-remove").ClickAsync();

        // Assert
        store.Count.Should().Be(0);
        await client.DidNotReceive().DeletePhotographAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
        cut.WaitForAssertion(() => cut.Find("#trip-photographs-empty").Should().NotBeNull());
    }

    [Fact]
    public async Task ItShouldDeleteASynchronisedPhotographOnTheServerToo()
    {
        // Arrange
        var store = new MemoryTripPhotographStore();
        var stored = StoredPhotograph(FirstPhotographId, syncStatus: SyncStatus.Synchronised);
        await store.SaveAsync(stored with { Bytes = [1, 2, 3] }, CancellationToken.None);
        var client = Substitute.For<ITripClient>();
        await using var context = CreateContext(store, tripClient: client);
        var cut = context.Render<TripPhotographsComponent>(parameters =>
            parameters.Add(component => component.Trip, Trip(stored))
                .Add(component => component.ViewerUserId, OwnerUserId));
        cut.WaitForAssertion(() => cut.Find("#trip-photo-remove").Should().NotBeNull());

        // Act
        await cut.Find("#trip-photo-remove").ClickAsync();

        // Assert
        await client.Received(1).DeletePhotographAsync(
            TripId,
            FirstPhotographId,
            Arg.Any<CancellationToken>());
        store.Count.Should().Be(0);
    }

    [Fact]
    public async Task ItShouldLeaveTheOtherPhotographsMetadataUntouched()
    {
        // Arrange
        var store = new MemoryTripPhotographStore();
        var capturedOn = StartedOn.AddMinutes(15);
        var removed = StoredPhotograph(FirstPhotographId, StartedOn.AddMinutes(5));
        var kept = StoredPhotograph(SecondPhotographId, capturedOn);
        await store.SaveAsync(kept with { Bytes = [1, 2, 3] }, CancellationToken.None);
        await store.SaveAsync(removed with { Bytes = [4, 5, 6] }, CancellationToken.None);
        await using var context = CreateContext(store);
        var cut = context.Render<TripPhotographsComponent>(parameters =>
            parameters.Add(component => component.Trip, Trip(removed, kept))
                .Add(component => component.ViewerUserId, OwnerUserId));
        cut.WaitForAssertion(() => cut.FindAll("#trip-photo-remove").Should().NotBeEmpty());

        // Act
        await cut.FindAll("#trip-photo-remove")[0].ClickAsync();

        // Assert
        store.Count.Should().Be(1);
        var survivor = store.Pending().Single();
        survivor.Id.Should().Be(SecondPhotographId);
        survivor.CapturedOn.Should().Be(capturedOn);
        survivor.OrderedOn.Should().Be(capturedOn);
    }

    [Fact]
    public async Task ItShouldKeepEachPhotographsMetadataIndependentAcrossSources()
    {
        // Arrange
        var store = new MemoryTripPhotographStore();
        var galleryCapturedOn = StartedOn.AddMinutes(20);
        await using var galleryContext = CreateContext(
            store,
            preparation: PreparationFor(
                Metadata(galleryCapturedOn, PhotographCapturedOnSourceEnum.ExifOriginal),
                PhotographSourceEnum.Gallery));
        var galleryCut = galleryContext.Render<TripPhotographsComponent>(parameters =>
            parameters.Add(component => component.Trip, Trip())
                .Add(component => component.ViewerUserId, OwnerUserId));
        galleryCut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("gallery.jpg"));
        galleryCut.WaitForAssertion(() => store.Count.Should().Be(1));
        var galleryId = store.Pending().Single().Id;

        // Act
        await using var cameraContext = CreateContext(
            store,
            preparation: PreparationFor(StrippedMetadata(), PhotographSourceEnum.Camera));
        var cameraCut = cameraContext.Render<TripPhotographsComponent>(parameters =>
            parameters.Add(component => component.Trip, Trip())
                .Add(component => component.ViewerUserId, OwnerUserId));
        cameraCut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("camera.jpg"));
        cameraCut.WaitForAssertion(() => store.Count.Should().Be(2));

        // Assert
        store.Stored(galleryId)!.CapturedOn.Should().Be(galleryCapturedOn);
        var cameraPhotograph = store.Pending().Single(photograph => photograph.Id != galleryId);
        cameraPhotograph.CapturedOn.Should().BeNull();
        store.Stored(galleryId)!.CapturedOn.Should().Be(galleryCapturedOn);
    }
}
