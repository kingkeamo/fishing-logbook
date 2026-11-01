using System.Globalization;
using AwesomeAssertions;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.Features.Trips.Clients;
using FishingLogBook.Mobile.Features.Trips.Services;
using FishingLogBook.Mobile.Features.Trips.ViewModels;
using FishingLogBook.Mobile.Interactions;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Mobile.Tests.Support;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Measurements;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using static FishingLogBook.Mobile.Tests.Features.Trips.TestSupport.TripTestData;

namespace FishingLogBook.Mobile.Tests.Features.Trips.Services.TripModalViewModelFactoryTests;

public sealed class WhenTestingCreate
{
    private readonly ServiceCollection _services = [];
    private readonly IConnectivityService _connectivity = Substitute.For<IConnectivityService>();
    private readonly ITripParticipantClient _participants = Substitute.For<ITripParticipantClient>();
    private readonly UiTextService _text = new(CultureInfo.GetCultureInfo("en-GB"));
    private readonly ITripModalViewModelFactory _sut;

    public WhenTestingCreate()
    {
        var units = Substitute.For<IAccountUnitsService>();
        units.GetAsync(Arg.Any<CancellationToken>()).Returns(MeasurementUnitsModel.Default);
        _connectivity.IsOnline.Returns(true);
        _services.AddSingleton(_text);
        _services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
        _services.AddSingleton(Substitute.For<ITripService>());
        _services.AddSingleton(Substitute.For<ITripCatchService>());
        _services.AddSingleton(Substitute.For<IAccountSessionCoordinator>());
        _services.AddSingleton(units);
        _services.AddSingleton<IMeasurementService>(new MeasurementService());
        _services.AddSingleton(_participants);
        _services.AddSingleton(Substitute.For<IAnglerLookupClient>());
        _services.AddSingleton(_connectivity);
        _services.AddSingleton(new InteractionService(Substitute.For<IInteractionPresenter>(), _text));
        _services.AddTripModalViewModels();
        _sut = _services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }).GetRequiredService<ITripModalViewModelFactory>();
    }

    [Fact]
    public void ItShouldRegisterEveryModalViewModelAsTransient()
    {
        // Arrange
        // Act
        var lifetimes = _services.Where(service => service.ServiceType == typeof(TripNoteEditorViewModel)
            || service.ServiceType == typeof(TripCatchPickerViewModel) || service.ServiceType == typeof(TripParticipantsViewModel))
            .Select(service => service.Lifetime).ToList();
        // Assert
        lifetimes.Should().HaveCount(3);
        lifetimes.Should().OnlyContain(lifetime => lifetime == ServiceLifetime.Transient);
    }

    [Fact]
    public void ItShouldHandOutAFreshStartPlannedTripViewModelOnEveryCall()
    {
        // Arrange
        // Act
        var first = _sut.CreateStartPlannedTrip();
        var second = _sut.CreateStartPlannedTrip();
        // Assert
        first.Should().NotBeSameAs(second);
        _services.Single(service => service.ServiceType == typeof(StartPlannedTripViewModel)).Lifetime
            .Should().Be(ServiceLifetime.Transient);
    }

    [Fact]
    public void ItShouldHandOutANewViewModelOfEachKindOnEveryCall()
    {
        // Arrange
        // Act
        var editors = (_sut.CreateNoteEditor(), _sut.CreateNoteEditor());
        var pickers = (_sut.CreateCatchPicker(), _sut.CreateCatchPicker());
        var participants = (_sut.CreateParticipants(), _sut.CreateParticipants());
        // Assert
        editors.Item1.Should().NotBeSameAs(editors.Item2);
        pickers.Item1.Should().NotBeSameAs(pickers.Item2);
        participants.Item1.Should().NotBeSameAs(participants.Item2);
    }

    [Fact]
    public void ItShouldNotLetASecondNoteEditorChangeWhatTheFirstIsEditing()
    {
        // Arrange
        var note = Note(Guid.NewGuid(), ViewerId, "Original");
        var first = _sut.CreateNoteEditor();
        first.Initialize(Trip(notes: [note]), note.Id);
        // Act
        var second = _sut.CreateNoteEditor();
        second.Initialize(Trip(), null);
        // Assert
        first.IsEditing.Should().BeTrue();
        first.NoteText.Should().Be("Original");
        second.IsEditing.Should().BeFalse();
        second.NoteText.Should().BeEmpty();
    }

    [Fact]
    public async Task ItShouldNotLetASecondParticipantsViewModelChangeWhatTheFirstShows()
    {
        // Arrange
        var otherTrip = Guid.NewGuid();
        _participants.GetAsync(TripId, Arg.Any<CancellationToken>()).Returns(ApiResultModel<TripParticipantsDto>.Succeeded(
            new TripParticipantsDto(TripId, TripParticipantConstants.Owner)
            {
                Participants = [new TripParticipantDto(ViewerId, TripParticipantConstants.Accepted, "Me", null, Now) { IsOwner = true }]
            }));
        _connectivity.IsOnline.Returns(true);
        var first = _sut.CreateParticipants();
        await first.LoadAsync(TripId, CancellationToken.None);
        var second = _sut.CreateParticipants();
        _participants.GetAsync(otherTrip, Arg.Any<CancellationToken>()).Returns(ApiResultModel<TripParticipantsDto>.Failed(FishingLogBook.Mobile.Api.Enums.ApiOutcomeEnum.Unavailable));
        // Act
        await second.LoadAsync(otherTrip, CancellationToken.None);
        // Assert
        first.Participants.Should().ContainSingle();
        first.CanInvite.Should().BeTrue();
        second.Participants.Should().BeEmpty();
        second.CanInvite.Should().BeFalse();
    }
}
