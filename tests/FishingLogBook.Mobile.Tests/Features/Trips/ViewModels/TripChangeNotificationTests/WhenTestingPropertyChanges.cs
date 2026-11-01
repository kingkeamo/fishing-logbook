using System.Globalization;
using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.Features.Trips.Clients;
using FishingLogBook.Mobile.Features.Trips.Models;
using FishingLogBook.Mobile.Features.Trips.Services;
using FishingLogBook.Mobile.Features.Trips.ViewModels;
using FishingLogBook.Mobile.Interactions;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Mobile.Navigation;
using FishingLogBook.Mobile.Tests.Support;
using FishingLogBook.Shared.Measurements;
using NSubstitute;
using static FishingLogBook.Mobile.Tests.Features.Trips.TestSupport.TripTestData;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.TripChangeNotificationTests;

/// <summary>Every trip view-model must tell its view when something changed, and keep working with nobody listening.</summary>
public sealed class WhenTestingPropertyChanges
{
    private readonly UiTextService _text = new(CultureInfo.GetCultureInfo("en-GB"));

    [Fact]
    public void ItShouldTellTheViewWhenTheNoteEditorChanges()
    {
        // Arrange
        var sut = new TripNoteEditorViewModel(Substitute.For<ITripService>(), Substitute.For<IAccountSessionCoordinator>(), _text, new FixedTimeProvider(Now));
        var changes = 0;
        sut.PropertyChanged += (_, _) => changes++;
        // Act
        sut.Initialize(Trip(), null);
        sut.NoteText = "Fish rising";
        // Assert
        changes.Should().Be(2);
        sut.SaveText.Should().Be(_text["TripNoteAdd"]);
    }

    [Fact]
    public async Task ItShouldTellTheViewWhenTheCatchPickerLoads()
    {
        // Arrange
        var catches = Substitute.For<ITripCatchService>();
        catches.GetEligibleAsync(Arg.Any<FishingLogBook.Mobile.Features.Trips.Offline.Models.LocalTripRecordModel>(), Arg.Any<CancellationToken>()).Returns([]);
        var units = Substitute.For<IAccountUnitsService>();
        units.GetAsync(Arg.Any<CancellationToken>()).Returns(MeasurementUnitsModel.Default);
        var sut = new TripCatchPickerViewModel(catches, units, new MeasurementService(), _text, new FixedTimeProvider(Now));
        var changes = 0;
        sut.PropertyChanged += (_, _) => changes++;
        // Act
        await sut.InitializeAsync(Trip(), CancellationToken.None);
        // Assert
        changes.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ItShouldTellTheViewWhenTheParticipantsLoad()
    {
        // Arrange
        var connectivity = Substitute.For<IConnectivityService>();
        connectivity.IsOnline.Returns(false);
        var sut = new TripParticipantsViewModel(Substitute.For<ITripParticipantClient>(), Substitute.For<IAnglerLookupClient>(), connectivity, Substitute.For<IAccountSessionCoordinator>(), new InteractionService(Substitute.For<IInteractionPresenter>(), _text), _text);
        var changes = 0;
        sut.PropertyChanged += (_, _) => changes++;
        // Act
        await sut.LoadAsync(TripId, CancellationToken.None);
        // Assert
        changes.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ItShouldTellTheViewWhenTheBannerRefreshes()
    {
        // Arrange
        var trips = Substitute.For<ITripService>();
        trips.LoadAsync(Arg.Any<CancellationToken>()).Returns(new TripHistoryModel([], new HashSet<Guid>(), false));
        var sut = new ActiveTripBannerViewModel(trips, Substitute.For<ITripStartService>(), Substitute.For<IAccountSessionCoordinator>(), new NavigationService(Substitute.For<IRouteNavigator>()), _text, new FixedTimeProvider(Now));
        var changes = 0;
        sut.PropertyChanged += (_, _) => changes++;
        // Act
        await sut.RefreshAsync(force: true, CancellationToken.None);
        // Assert
        changes.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ItShouldKeepWorkingWhenNobodyListensToTheTripAssociationChanges()
    {
        // Arrange
        var service = Substitute.For<ITripAssociationService>();
        service.ResolveAsync(Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(new TripAssociationModel(TripId, "Corrib", null, Now, [new TripAnglerOptionModel(ViewerId, "Me", true)]));
        var sut = new TripAssociationViewModel(service, _text);
        await sut.InitializeNewAsync(ViewerId, null, CancellationToken.None);
        // Act
        sut.Leave();
        sut.Rejoin();
        sut.SelectAngler(ViewerId.ToString("D"));
        // Assert
        sut.ShowAssociation.Should().BeTrue();
        sut.TripName.Should().Be("Corrib");
    }

    [Fact]
    public async Task ItShouldHandleASessionActivationWhileNobodyListens()
    {
        // Arrange
        var session = Substitute.For<IAccountSessionCoordinator>();
        var trips = Substitute.For<ITripService>();
        trips.LoadAsync(Arg.Any<CancellationToken>()).Returns(new TripHistoryModel([], new HashSet<Guid>(), false));
        var sut = new ActiveTripBannerViewModel(trips, Substitute.For<ITripStartService>(), session, new NavigationService(Substitute.For<IRouteNavigator>()), _text, new FixedTimeProvider(Now));
        // Act
        session.SessionActivated += Raise.Event();
        await Task.Delay(100);
        // Assert
        await trips.Received(1).LoadAsync(Arg.Any<CancellationToken>());
        sut.CanStart.Should().BeTrue();
    }
}
