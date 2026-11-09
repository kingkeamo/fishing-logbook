using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Import.Enums;
using FishingLogBook.Mobile.Features.Import.Models;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Import.ViewModels.ImportViewModelTests;

public sealed class WhenTestingLocationAndTripActions : BaseImportCatchActionsTest
{
    private static ImportLocationModel ResolvedLocation()
    {
        return new ImportLocationModel(53.3498, -6.2603, true)
            .WithLookup(
                ImportLocationLookupStatusEnum.Resolved,
                new ImportLocationLookupResultModel(["Main Street", "Dublin", "Ireland"]));
    }

    [Fact]
    public async Task ItShouldAcceptTheHistoricalLocationOfACatch()
    {
        // Arrange
        await ReviewPreparedPhotosAsync(PhotoAt(FirstPhotoId, 0, 0, ResolvedLocation()));
        var row = Sut.Catches.Single();

        // Act
        Sut.AcceptLocation(row.Proposal.Id, row.Proposal.Location!);

        // Assert
        var location = Sut.Catches.Single().Proposal.Location!;
        location.Decision.Should().Be(ImportLocationDecisionEnum.Accepted);
        location.Latitude.Should().Be(53.3498);
        location.Longitude.Should().Be(-6.2603);
        location.PlaceName.Should().Be("Main Street, Dublin, Ireland");
    }

    [Fact]
    public async Task ItShouldRemoveAnAcceptedLocationAndItsPlaceName()
    {
        // Arrange
        await ReviewPreparedPhotosAsync(PhotoAt(FirstPhotoId, 0, 0, ResolvedLocation()));
        var row = Sut.Catches.Single();
        Sut.AcceptLocation(row.Proposal.Id, row.Proposal.Location!);

        // Act
        Sut.RemoveLocation(row.Proposal.Id);

        // Assert
        var location = Sut.Catches.Single().Proposal.Location!;
        location.Decision.Should().Be(ImportLocationDecisionEnum.Removed);
        location.PlaceName.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldDoNothingWhenRemovingALocationFromACatchWithoutOne()
    {
        // Arrange
        await ReviewPreparedPhotoAsync(ReadyPhoto());
        var catchId = Sut.Catches.Single().Proposal.Id;

        // Act
        Sut.RemoveLocation(catchId);

        // Assert
        Sut.Catches.Single().Proposal.Location.Should().BeNull();
    }

    [Fact]
    public async Task ItShouldNotOpenThePlaceNameModalWhenTheCatchHasNoLocation()
    {
        // Arrange
        await ReviewPreparedPhotoAsync(ReadyPhoto());
        var catchId = Sut.Catches.Single().Proposal.Id;

        // Act
        await Sut.EditPlaceNameAsync(catchId);

        // Assert
        await MockModals.DidNotReceive().EditPlaceNameAsync(Arg.Any<PlaceNameModalModel>());
    }

    [Fact]
    public async Task ItShouldNotOpenThePlaceNameModalWhenThereAreNoPlaceNameComponents()
    {
        // Arrange
        await ReviewPreparedPhotosAsync(PhotoAt(FirstPhotoId, 0, 0, new ImportLocationModel(53.3498, -6.2603, true)));
        var catchId = Sut.Catches.Single().Proposal.Id;

        // Act
        await Sut.EditPlaceNameAsync(catchId);

        // Assert
        await MockModals.DidNotReceive().EditPlaceNameAsync(Arg.Any<PlaceNameModalModel>());
    }

    [Fact]
    public async Task ItShouldKeepThePlaceNameWhenThePlaceNameModalIsDismissed()
    {
        // Arrange
        await ReviewPreparedPhotosAsync(PhotoAt(FirstPhotoId, 0, 0, ResolvedLocation()));
        var catchId = Sut.Catches.Single().Proposal.Id;
        MockModals.EditPlaceNameAsync(Arg.Any<PlaceNameModalModel>()).Returns((PlaceNameModalResult?)null);

        // Act
        await Sut.EditPlaceNameAsync(catchId);

        // Assert
        Sut.Catches.Single().Proposal.Location!.PlaceName.Should().Be("Main Street, Dublin, Ireland");
    }

    [Fact]
    public async Task ItShouldOfferTheResolvedComponentsAndApplyTheChosenPlaceName()
    {
        // Arrange
        await ReviewPreparedPhotosAsync(PhotoAt(FirstPhotoId, 0, 0, ResolvedLocation()));
        var catchId = Sut.Catches.Single().Proposal.Id;
        MockModals.EditPlaceNameAsync(Arg.Any<PlaceNameModalModel>()).Returns(new PlaceNameModalResult("Dublin, Ireland"));

        // Act
        await Sut.EditPlaceNameAsync(catchId);

        // Assert
        Sut.Catches.Single().Proposal.Location!.PlaceName.Should().Be("Dublin, Ireland");
        await MockModals.Received(1).EditPlaceNameAsync(
            Arg.Is<PlaceNameModalModel>(model =>
                model.Components.SequenceEqual(new[] { "Main Street", "Dublin", "Ireland" })
                && model.CurrentPlaceName == "Main Street, Dublin, Ireland"));
    }

    [Fact]
    public async Task ItShouldKeepACatchOutOfItsTripWhenKeepingItSeparate()
    {
        // Arrange
        await ReachTripReviewAsync();
        var tripId = Sut.Trips.Single().Proposal.Id;

        // Act
        Sut.DecideKeepSeparate(tripId);

        // Assert
        Sut.Trips.Single().Proposal.Decision.Should().Be(ImportTripDecisionEnum.NoTrip);
        Sut.Trips.Single().Proposal.IsDecisionComplete.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldRemoveACatchFromItsSuggestedTrip()
    {
        // Arrange
        await ReachTripReviewAsync();
        var trip = Sut.Trips.Single().Proposal;
        var catchId = trip.CatchProposalIds.Single();

        // Act
        Sut.RemoveCatchFromTrip(trip.Id, catchId);

        // Assert
        Sut.Trips.SelectMany(row => row.Proposal.CatchProposalIds).Should().NotContain(catchId);
    }

    [Fact]
    public async Task ItShouldRefuseToChangeATripThatDoesNotExist()
    {
        // Arrange
        await ReachTripReviewAsync();

        // Act
        var keep = () => Sut.DecideKeepSeparate(Guid.NewGuid());
        var remove = () => Sut.RemoveCatchFromTrip(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        keep.Should().Throw<InvalidOperationException>();
        remove.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ItShouldNotThrowWhenPersistenceIsCancelledBeforeItStarts()
    {
        // Arrange
        // Act
        var act = () => Sut.CancelPersistence();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task ItShouldReplaceTheShownValuesWithTheCanonicalOnesWhenTheCatchIsUsedAsShown()
    {
        // Arrange
        await ReviewPreparedPhotosAsync(PhotoAt(FirstPhotoId, 0, 0), PhotoAt(SecondPhotoId, 1, 20));
        var primary = Sut.Catches[0].Proposal.Id;
        Sut.MergeCatches(primary, Sut.Catches[1].Proposal.Id);
        Sut.Catches.Single().NeedsCanonicalReview.Should().BeTrue();

        // Act
        Sut.UseShownValues(primary);

        // Assert
        Sut.Catches.Single().NeedsCanonicalReview.Should().BeFalse();
    }
}
