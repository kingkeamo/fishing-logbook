using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Trips.Clients;
using FishingLogBook.Web.Features.Trips.Enums;
using FishingLogBook.Web.Features.Trips.Modals.AddTripCatches;
using FishingLogBook.Web.Features.Trips.Modals.AddTripNote;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
using FishingLogBook.Web.Features.Trips.Services;
using FishingLogBook.Web.Localization;
using NSubstitute;
using ActiveTripPage = FishingLogBook.Web.Features.Trips.Pages.ActiveTrip.ActiveTrip;

namespace FishingLogBook.Web.Tests.Features.Trips.Pages.ActiveTripTests;

public class WhenTestingNoteAndCatchActions : BaseActiveTripTest
{
    private static readonly Guid NoteId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    private static ITripStore StoreWithNote()
    {
        var store = Substitute.For<ITripStore>();
        var note = new TripNoteModel(NoteId, TripId, OwnerUserId, "water dropped", StartedOn.AddHours(1));
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>())
            .Returns(StoredActiveTrip() with { Notes = [note] });
        return store;
    }

    private static ITripClient ClientWithActiveServerTripAndNote()
    {
        var detail = new TripDetailDto(new TripViewDto(TripId, OwnerUserId, TripConstants.Active, StartedOn, null))
        {
            Role = TripParticipantConstants.Owner,
            Notes =
            [
                new TripNoteDto(NoteId, TripId, "water dropped", StartedOn.AddHours(1)) { CreatedByUserId = OwnerUserId }
            ]
        };
        var client = Substitute.For<ITripClient>();
        client.GetDetailAsync(TripId, Arg.Any<CancellationToken>()).Returns(detail);
        return client;
    }

    private static ITripStore EmptyStore()
    {
        var store = Substitute.For<ITripStore>();
        store.GetAsync(OwnerUserId, TripId, Arg.Any<CancellationToken>()).Returns((TripModel?)null);
        return store;
    }

    [Fact]
    public async Task ItShouldOpenTheNoteEditorForTheChosenNote()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var modalService = ConfirmingModalService();
        await using var context = CreateContext(StoreWithNote(), modalService: modalService);
        var cut = context.Render<ActiveTripPage>(parameters => parameters.Add(page => page.TripId, TripId));
        cut.WaitForAssertion(() => cut.Find($"#trip-timeline-note-edit-{NoteId:D}").Should().NotBeNull());

        // Act
        await cut.Find($"#trip-timeline-note-edit-{NoteId:D}").ClickAsync();

        // Assert
        await modalService.Received(1)
            .ShowAsync<AddTripNoteModal, AddTripNoteModalModel, AddTripNoteModalResult>(
                Arg.Is<AddTripNoteModalModel>(model => model.TripId == TripId
                    && model.ExistingNote != null
                    && model.ExistingNote.Id == NoteId
                    && model.ExistingNote.Text == "water dropped"),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRemoveTheChosenNoteAfterConfirmation()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var modalService = ConfirmingModalService();
        var noteWriter = Substitute.For<ITripNoteWriteService>();
        await using var context = CreateContext(
            EmptyStore(),
            modalService: modalService,
            noteWriter: noteWriter,
            tripClient: ClientWithActiveServerTripAndNote());
        var cut = context.Render<ActiveTripPage>(parameters => parameters.Add(page => page.TripId, TripId));
        cut.WaitForAssertion(() => cut.Find($"#trip-timeline-note-remove-{NoteId:D}").Should().NotBeNull());

        // Act
        await cut.Find($"#trip-timeline-note-remove-{NoteId:D}").ClickAsync();

        // Assert
        await modalService.Received(1).ConfirmAsync(Arg.Any<ConfirmModalModel>(), Arg.Any<CancellationToken>());
        await noteWriter.Received(1).RemoveAsync(
            Arg.Is<TripNoteRemovalModel>(removal => removal.NoteId == NoteId && removal.TripId == TripId),
            TripStorageEnum.Server,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepTheNoteWhenTheRemovalIsNotConfirmed()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var modalService = ConfirmingModalService(confirm: false);
        var noteWriter = Substitute.For<ITripNoteWriteService>();
        await using var context = CreateContext(
            EmptyStore(),
            modalService: modalService,
            noteWriter: noteWriter,
            tripClient: ClientWithActiveServerTripAndNote());
        var cut = context.Render<ActiveTripPage>(parameters => parameters.Add(page => page.TripId, TripId));
        cut.WaitForAssertion(() => cut.Find($"#trip-timeline-note-remove-{NoteId:D}").Should().NotBeNull());

        // Act
        await cut.Find($"#trip-timeline-note-remove-{NoteId:D}").ClickAsync();

        // Assert
        cut.Find($"#trip-timeline-note-remove-{NoteId:D}").Should().NotBeNull();
        await noteWriter.DidNotReceive().RemoveAsync(
            Arg.Any<TripNoteRemovalModel>(),
            Arg.Any<TripStorageEnum>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldOpenTheExistingCatchPickerForTheTrip()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var modalService = ConfirmingModalService();
        await using var context = CreateContext(StoreWithNote(), modalService: modalService);
        var cut = context.Render<ActiveTripPage>(parameters => parameters.Add(page => page.TripId, TripId));
        cut.WaitForAssertion(() => cut.Find("#active-trip-add-catch").Should().NotBeNull());

        // Act
        await cut.Find("#active-trip-add-catch").ClickAsync();

        // Assert
        await modalService.Received(1)
            .ShowAsync<AddTripCatchesModal, AddTripCatchesModalModel, AddTripCatchesModalResult>(
                Arg.Is<AddTripCatchesModalModel>(model => model.Scope.TripId == TripId
                    && model.Scope.OwnerUserId == OwnerUserId),
                Arg.Any<CancellationToken>());
    }
}
