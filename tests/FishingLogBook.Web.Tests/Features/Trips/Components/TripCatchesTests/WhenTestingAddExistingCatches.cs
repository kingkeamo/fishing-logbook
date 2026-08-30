using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Features.Trips.Enums;
using FishingLogBook.Web.Features.Trips.Modals.AddTripCatches;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.TestSupport;
using NSubstitute;
using TripCatchesComponent = FishingLogBook.Web.Features.Trips.Components.TripCatches.TripCatches;

namespace FishingLogBook.Web.Tests.Features.Trips.Components.TripCatchesTests;

public class WhenTestingAddExistingCatches : BaseTripCatchesTest
{
    [Fact]
    public async Task ItShouldOfferRecordCatchForThisTripWithoutOpeningThePicker()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var modalService = ModalServiceAdding();
        await using var context = CreateContext(modalService);

        // Act
        var cut = context.Render<TripCatchesComponent>(parameters =>
            parameters.Add(component => component.Trip, Trip())
                .Add(component => component.ViewerUserId, OwnerUserId));

        // Assert
        cut.Find("#trip-catches-record").GetAttribute("href").Should().Be($"/catches/record?tripId={TripId:D}");
        cut.Find("#trip-catches-add").TextContent.Should().Contain("Add catch");
        cut.Find("#trip-catches-actions").ClassName.Should().Contain("mud-grid");
        await modalService.DidNotReceive()
            .ShowAsync<AddTripCatchesModal, AddTripCatchesModalModel, AddTripCatchesModalResult>(
                Arg.Any<AddTripCatchesModalModel>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldOpenThePickerForThisTripsTimeframe()
    {
        // Arrange
        var modalService = ModalServiceAdding();
        await using var context = CreateContext(modalService);
        var cut = context.Render<TripCatchesComponent>(parameters =>
            parameters.Add(component => component.Trip, CompletedTrip())
                .Add(component => component.ViewerUserId, OwnerUserId));

        // Act
        await cut.Find("#trip-catches-add").ClickAsync();

        // Assert
        await modalService.Received(1)
            .ShowAsync<AddTripCatchesModal, AddTripCatchesModalModel, AddTripCatchesModalResult>(
                Arg.Is<AddTripCatchesModalModel>(model =>
                    model.Scope.TripId == TripId
                    && model.Scope.OwnerUserId == OwnerUserId
                    && model.Scope.StartedOn == StartedOn
                    && model.Scope.EndedOn == EndedOn
                    && model.Storage == TripStorageEnum.LocalFirst),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAskTheServerForAHistoricalTrip()
    {
        // Arrange
        var modalService = ModalServiceAdding();
        await using var context = CreateContext(modalService);
        var cut = context.Render<TripCatchesComponent>(parameters => parameters
            .Add(component => component.Trip, CompletedTrip())
            .Add(component => component.ViewerUserId, OwnerUserId)
            .Add(component => component.CatchStorage, TripStorageEnum.Server));

        // Act
        await cut.Find("#trip-catches-add").ClickAsync();

        // Assert
        await modalService.Received(1)
            .ShowAsync<AddTripCatchesModal, AddTripCatchesModalModel, AddTripCatchesModalResult>(
                Arg.Is<AddTripCatchesModalModel>(model => model.Storage == TripStorageEnum.Server),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotTellTheParentWhenThePickerWasDismissed()
    {
        // Arrange
        var attached = 0;
        await using var context = CreateContext(ModalServiceAdding());
        var cut = context.Render<TripCatchesComponent>(parameters => parameters
            .Add(component => component.Trip, Trip())
            .Add(component => component.ViewerUserId, OwnerUserId)
            .Add(component => component.OnCatchesAttached, () => attached++));

        // Act
        await cut.Find("#trip-catches-add").ClickAsync();

        // Assert
        attached.Should().Be(0);
        cut.FindAll("#trip-catches-partial").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldTellTheParentWhenCatchesWereAdded()
    {
        // Arrange
        var attached = 0;
        await using var context = CreateContext(ModalServiceAdding(
            new AddTripCatchesModalResult([PikeCatchId, TroutCatchId], [])));
        var cut = context.Render<TripCatchesComponent>(parameters => parameters
            .Add(component => component.Trip, Trip())
            .Add(component => component.ViewerUserId, OwnerUserId)
            .Add(component => component.OnCatchesAttached, () => attached++));

        // Act
        await cut.Find("#trip-catches-add").ClickAsync();

        // Assert
        attached.Should().Be(1);
        cut.FindAll("#trip-catches-partial").Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldWarnWhenSomeCatchesCouldNotBeAdded()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var attached = 0;
        await using var context = CreateContext(ModalServiceAdding(
            new AddTripCatchesModalResult([PikeCatchId], [TroutCatchId])));
        var cut = context.Render<TripCatchesComponent>(parameters => parameters
            .Add(component => component.Trip, Trip())
            .Add(component => component.ViewerUserId, OwnerUserId)
            .Add(component => component.OnCatchesAttached, () => attached++));

        // Act
        await cut.Find("#trip-catches-add").ClickAsync();

        // Assert
        attached.Should().Be(1);
        cut.Find("#trip-catches-partial").TextContent.Should().Contain("no longer available");
    }
}
