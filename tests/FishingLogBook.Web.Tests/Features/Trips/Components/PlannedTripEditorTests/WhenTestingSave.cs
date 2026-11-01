using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Trips.Components.PlannedTripEditor;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FishingLogBook.Web.Tests.Features.Trips.Components.PlannedTripEditorTests;

public class WhenTestingSave : BasePlannedTripEditorTest
{
    [Fact]
    public async Task ItShouldRequireAPlannedDateBeforeSaving()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext();
        var cut = context.Render<PlannedTripEditor>(parameters => parameters
            .Add(editor => editor.ViewerUserId, OwnerUserId));

        // Act
        await cut.Find("#planned-trip-save").ClickAsync();

        // Assert
        cut.Find("#planned-trip-when-invalid").Should().NotBeNull();
        await PlannedTrips.DidNotReceive().PlanAsync(
            Arg.Any<Guid>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
        await Synchroniser.DidNotReceive().SynchronisePendingAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportAFailedSaveAndLogIt()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var failure = new InvalidOperationException("store");
        PlannedTrips.PlanAsync(
                Arg.Any<Guid>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(failure);
        await using var context = CreateContext();
        var cut = context.Render<PlannedTripEditor>(parameters => parameters
            .Add(editor => editor.ViewerUserId, OwnerUserId));
        cut.Find("#planned-trip-when").Input("2026-09-12T06:30");

        // Act
        await cut.Find("#planned-trip-save").ClickAsync();

        // Assert
        cut.Find("#planned-trip-save-failed").Should().NotBeNull();
        await Logging.Received(1).LogErrorAsync("saving a planned trip", failure, Arg.Any<CancellationToken>());
        await Synchroniser.DidNotReceive().SynchronisePendingAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportAFailureWhenThePlanCanNoLongerBeUpdated()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        TripStore.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns(Planned());
        PlannedTrips.UpdatePlanAsync(
                Arg.Any<Guid>(),
                Arg.Any<Guid>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .Returns((TripModel?)null);
        await using var context = CreateContext();
        var cut = context.Render<PlannedTripEditor>(parameters => parameters
            .Add(editor => editor.ViewerUserId, OwnerUserId)
            .Add(editor => editor.TripId, TripId));
        cut.WaitForAssertion(() => cut.Find("#planned-trip-save").Should().NotBeNull());

        // Act
        await cut.Find("#planned-trip-save").ClickAsync();

        // Assert
        cut.Find("#planned-trip-save-failed").Should().NotBeNull();
        await Synchroniser.DidNotReceive().SynchronisePendingAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepTheSavedPlanWhenSynchronisingFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var failure = new HttpRequestException("offline");
        Synchroniser.SynchronisePendingAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).ThrowsAsync(failure);
        PlannedTrips.PlanAsync(
                OwnerUserId,
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .Returns(Planned());
        await using var context = CreateContext();
        var cut = context.Render<PlannedTripEditor>(parameters => parameters
            .Add(editor => editor.ViewerUserId, OwnerUserId));
        cut.Find("#planned-trip-when").Input("2026-09-12T06:30");

        // Act
        await cut.Find("#planned-trip-save").ClickAsync();

        // Assert
        cut.FindAll("#planned-trip-save-failed").Should().BeEmpty();
        await Logging.Received(1).LogErrorAsync("synchronising a planned trip", failure, Arg.Any<CancellationToken>());
        context.Services.GetRequiredService<NavigationManager>().Uri.Should().EndWith($"/trips/{TripId:D}/plan");
    }

    [Fact]
    public async Task ItShouldPlanANewTripSynchroniseAndOpenIt()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        PlannedTrips.PlanAsync(
                OwnerUserId,
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .Returns(Planned());
        await using var context = CreateContext();
        var cut = context.Render<PlannedTripEditor>(parameters => parameters
            .Add(editor => editor.ViewerUserId, OwnerUserId)
            .Add(editor => editor.TripBaseHref, "/offline/trips"));
        cut.Find("#planned-trip-title").Input("Corrib");
        cut.Find("#planned-trip-when").Input("2026-09-12T06:30");

        // Act
        await cut.Find("#planned-trip-save").ClickAsync();

        // Assert
        await PlannedTrips.Received(1).PlanAsync(
            OwnerUserId,
            "Corrib",
            null,
            PlannedStart,
            Arg.Any<CancellationToken>());
        await Synchroniser.Received(1).SynchronisePendingAsync(OwnerUserId, Arg.Any<CancellationToken>());
        context.Services.GetRequiredService<NavigationManager>().Uri
            .Should().EndWith($"/offline/trips/{TripId:D}/plan");
    }

    [Fact]
    public async Task ItShouldUpdateAnExistingPlanAndSynchroniseIt()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var updated = Planned() with { Title = "Renamed" };
        TripStore.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns(Planned(), updated);
        PlannedTrips.UpdatePlanAsync(
                OwnerUserId,
                TripId,
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .Returns(updated);
        await using var context = CreateContext();
        var cut = context.Render<PlannedTripEditor>(parameters => parameters
            .Add(editor => editor.ViewerUserId, OwnerUserId)
            .Add(editor => editor.TripId, TripId));
        cut.WaitForAssertion(() => cut.Find("#planned-trip-title").GetAttribute("value").Should().Be("Corrib"));
        cut.Find("#planned-trip-title").Input("Renamed");
        cut.Find("#planned-trip-when").Input("2026-09-13T08:00");

        // Act
        await cut.Find("#planned-trip-save").ClickAsync();

        // Assert
        await PlannedTrips.Received(1).UpdatePlanAsync(
            OwnerUserId,
            TripId,
            "Renamed",
            "Shore",
            DateTimeOffset.Parse("2026-09-13T08:00:00Z"),
            Arg.Any<CancellationToken>());
        await Synchroniser.Received(1).SynchronisePendingAsync(OwnerUserId, Arg.Any<CancellationToken>());
        cut.FindAll("#planned-trip-save-failed").Should().BeEmpty();
    }
}
