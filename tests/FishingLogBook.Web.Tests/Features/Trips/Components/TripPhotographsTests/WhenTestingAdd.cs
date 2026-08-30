using AwesomeAssertions;
using Bunit;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Photographs.Enums;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.Features.Trips.Offline.Stores.TripPhotographStoreTests;
using Microsoft.AspNetCore.Components.Forms;
using NSubstitute;
using TripPhotographsComponent =
    FishingLogBook.Web.Features.Trips.Components.TripPhotographs.TripPhotographs;

namespace FishingLogBook.Web.Tests.Features.Trips.Components.TripPhotographsTests;

public class WhenTestingAdd : BaseTripPhotographsTest
{
    [Fact]
    public async Task ItShouldOfferTheAddActionWithNoPhotographsYet()
    {
        // Arrange
        var store = new MemoryTripPhotographStore();
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<TripPhotographsComponent>(parameters =>
            parameters.Add(component => component.Trip, Trip())
                .Add(component => component.ViewerUserId, OwnerUserId));

        // Assert
        cut.Find("#trip-photographs").Should().NotBeNull();
        cut.Find("#trip-photographs-empty").Should().NotBeNull();
        cut.FindComponents<InputFile>().Should().NotBeEmpty();
        cut.FindAll("#trip-photo-carousel").Should().BeEmpty();
        store.Count.Should().Be(0);
    }

    [Fact]
    public async Task ItShouldRejectAPhotographThatCannotBeSanitised()
    {
        // Arrange
        var store = new MemoryTripPhotographStore();
        await using var context = CreateContext(store, preparation: FailingPreparation());
        var cut = context.Render<TripPhotographsComponent>(parameters =>
            parameters.Add(component => component.Trip, Trip())
                .Add(component => component.ViewerUserId, OwnerUserId));

        // Act
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("photo.jpg"));

        // Assert
        store.Count.Should().Be(0);
        cut.FindAll("#trip-photo-carousel").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldShowAFailureWhenTheLocalWriteFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var store = new MemoryTripPhotographStore { FailWrite = true };
        var logging = Substitute.For<Web.Features.Diagnostics.Services.ILoggingService>();
        await using var context = CreateContext(store, logging: logging);
        var cut = context.Render<TripPhotographsComponent>(parameters =>
            parameters.Add(component => component.Trip, Trip())
                .Add(component => component.ViewerUserId, OwnerUserId));

        // Act
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("photo.jpg"));

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#trip-photo-add-failed").TextContent
                .Should().Contain("could not be added"));
        store.Count.Should().Be(0);
        await logging.Received(1).LogErrorAsync(
            "adding a trip photograph",
            Arg.Any<Exception>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStoreOnlyTheAddedTimeWhenThePlatformStrippedTheMetadata()
    {
        // Arrange
        var store = new MemoryTripPhotographStore();
        await using var context = CreateContext(store, preparation: PreparationFor(StrippedMetadata()));
        var cut = context.Render<TripPhotographsComponent>(parameters =>
            parameters.Add(component => component.Trip, Trip())
                .Add(component => component.ViewerUserId, OwnerUserId));

        // Act
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("photo.jpg"));

        // Assert
        cut.WaitForAssertion(() => store.Count.Should().Be(1));
        var stored = store.Stored(store.Pending().Single().Id)!;
        stored.CapturedOn.Should().BeNull();
        stored.AddedOn.Should().NotBe(default);
        stored.OrderedOn.Should().Be(stored.AddedOn);
        stored.TripId.Should().Be(TripId);
        stored.ContributedByUserId.Should().Be(OwnerUserId);
    }

    [Fact]
    public async Task ItShouldNotTreatAFileTimestampAsACaptureTime()
    {
        // Arrange
        var store = new MemoryTripPhotographStore();
        var fileTimestamp = StartedOn.AddHours(5);
        await using var context = CreateContext(
            store,
            preparation: PreparationFor(
                Metadata(fileTimestamp, PhotographCapturedOnSourceEnum.FileLastModified)));
        var cut = context.Render<TripPhotographsComponent>(parameters =>
            parameters.Add(component => component.Trip, Trip())
                .Add(component => component.ViewerUserId, OwnerUserId));

        // Act
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("photo.jpg"));

        // Assert
        cut.WaitForAssertion(() => store.Count.Should().Be(1));
        store.Pending().Single().CapturedOn.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldKeepATrustworthyExifCaptureTime()
    {
        // Arrange
        var store = new MemoryTripPhotographStore();
        var capturedOn = StartedOn.AddMinutes(40);
        await using var context = CreateContext(
            store,
            preparation: PreparationFor(
                Metadata(capturedOn, PhotographCapturedOnSourceEnum.ExifOriginal)));
        var cut = context.Render<TripPhotographsComponent>(parameters =>
            parameters.Add(component => component.Trip, Trip())
                .Add(component => component.ViewerUserId, OwnerUserId));

        // Act
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("photo.jpg"));

        // Assert
        cut.WaitForAssertion(() => store.Count.Should().Be(1));
        var stored = store.Pending().Single();
        stored.CapturedOn.Should().Be(capturedOn);
        stored.OrderedOn.Should().Be(capturedOn);
    }

    [Fact]
    public async Task ItShouldNeverRenderPhotographCoordinates()
    {
        // Arrange
        var store = new MemoryTripPhotographStore();
        await using var context = CreateContext(
            store,
            preparation: PreparationFor(
                Metadata(
                    StartedOn,
                    PhotographCapturedOnSourceEnum.ExifOriginal,
                    latitude: 53.2707,
                    longitude: -9.0568)));
        var cut = context.Render<TripPhotographsComponent>(parameters =>
            parameters.Add(component => component.Trip, Trip())
                .Add(component => component.ViewerUserId, OwnerUserId));

        // Act
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("photo.jpg"));

        // Assert
        cut.WaitForAssertion(() => store.Count.Should().Be(1));
        cut.Markup.Should().NotContain("53.2707");
        cut.Markup.Should().NotContain("-9.0568");
    }

    [Fact]
    public async Task ItShouldSaveAGalleryPhotographLocallyAndShowIt()
    {
        // Arrange
        var store = new MemoryTripPhotographStore();
        await using var context = CreateContext(store);
        var cut = context.Render<TripPhotographsComponent>(parameters =>
            parameters.Add(component => component.Trip, Trip())
                .Add(component => component.ViewerUserId, OwnerUserId));

        // Act
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("photo.jpg"));

        // Assert
        cut.WaitForAssertion(() =>
        {
            cut.Find("#trip-photo-carousel").Should().NotBeNull();
            cut.FindAll("#trip-photographs-empty").Should().BeEmpty();
        });
        store.Count.Should().Be(1);
        var stored = store.Pending().Single();
        stored.SyncStatus.Should().Be(SyncStatus.SavedLocally);
        stored.TripId.Should().Be(TripId);
    }

    [Fact]
    public async Task ItShouldAddSeveralPhotographsIndependently()
    {
        // Arrange
        var store = new MemoryTripPhotographStore();
        await using var context = CreateContext(store);
        var cut = context.Render<TripPhotographsComponent>(parameters =>
            parameters.Add(component => component.Trip, Trip())
                .Add(component => component.ViewerUserId, OwnerUserId));

        // Act
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("one.jpg"));
        cut.WaitForAssertion(() => store.Count.Should().Be(1));
        cut.FindComponents<InputFile>()[0].UploadFiles(JpegFile("two.jpg"));

        // Assert
        cut.WaitForAssertion(() => store.Count.Should().Be(2));
        store.Pending().Select(photograph => photograph.Id).Distinct().Should().HaveCount(2);
        store.Pending().Should().AllSatisfy(photograph =>
            photograph.TripId.Should().Be(TripId));
    }

    [Fact]
    public async Task ItShouldShowStoredPhotographsForTheTripOnly()
    {
        // Arrange
        var mine = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var store = new MemoryTripPhotographStore();
        await store.SaveAsync(
            StoredPhotograph(mine) with { Bytes = [1, 2, 3] },
            CancellationToken.None);
        await store.SaveAsync(
            new TripPhotographModel(
                Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
                Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),
                OwnerUserId,
                "image/jpeg",
                StartedOn,
                Bytes: [4, 5, 6]),
            CancellationToken.None);
        await using var context = CreateContext(store);

        // Act
        var cut = context.Render<TripPhotographsComponent>(parameters =>
            parameters.Add(component => component.Trip, Trip(StoredPhotograph(mine)))
                .Add(component => component.ViewerUserId, OwnerUserId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#trip-photo-carousel").Should().NotBeNull());
        store.BytesReadFor.Should().Equal(mine);
    }
}
