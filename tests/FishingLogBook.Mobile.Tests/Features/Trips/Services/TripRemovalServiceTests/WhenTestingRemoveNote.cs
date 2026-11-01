using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using FishingLogBook.Mobile.Interactions;
using FishingLogBook.Shared.Dtos;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using static FishingLogBook.Mobile.Tests.Features.Trips.TestSupport.TripTestData;

namespace FishingLogBook.Mobile.Tests.Features.Trips.Services.TripRemovalServiceTests;

public sealed class WhenTestingRemoveNote : BaseTripRemovalServiceTest
{
    private static readonly Guid NoteId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    [Fact]
    public async Task ItShouldNeitherAskNorSaveWhenTheNoteIsNoLongerOnTheTrip()
    {
        // Arrange
        var trip = Trip(notes: [Note(Guid.NewGuid(), ViewerId)]);
        // Act
        var removed = await Sut.RemoveNoteAsync(trip, NoteId, CancellationToken.None);
        // Assert
        removed.Should().BeFalse();
        await MockPresenter.DidNotReceive().ShowDialogAsync(Arg.Any<DialogRequest>(), Arg.Any<CancellationToken>());
        await MockTrips.DidNotReceive().SaveNoteAsync(Arg.Any<LocalTripRecordModel>(), Arg.Any<TripNoteDto>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldChangeNothingAndStaySilentWhenTheAnglerDeclinesTheConfirmation()
    {
        // Arrange
        AnswerConfirmation(false);
        // Act
        var removed = await Sut.RemoveNoteAsync(Trip(notes: [Note(NoteId, ViewerId)]), NoteId, CancellationToken.None);
        // Assert
        removed.Should().BeFalse();
        await MockTrips.DidNotReceive().SaveNoteAsync(Arg.Any<LocalTripRecordModel>(), Arg.Any<TripNoteDto>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
        await MockPresenter.DidNotReceive().NotifyAsync(Arg.Any<NotificationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldTellTheAnglerAndKeepTheNoteWhenItCannotBeRemoved()
    {
        // Arrange
        MockTrips.SaveNoteAsync(Arg.Any<LocalTripRecordModel>(), Arg.Any<TripNoteDto>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException());
        // Act
        var removed = await Sut.RemoveNoteAsync(Trip(notes: [Note(NoteId, ViewerId)]), NoteId, CancellationToken.None);
        // Assert
        removed.Should().BeFalse();
        await MockPresenter.Received(1).NotifyAsync(
            Arg.Is<NotificationRequest>(request => request.Kind == NotificationKindEnum.Error && request.Message.Contains(Text["TripNoteRemoveFailed"])),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotHideACancellationOrReportItAsAFailure()
    {
        // Arrange
        MockTrips.SaveNoteAsync(Arg.Any<LocalTripRecordModel>(), Arg.Any<TripNoteDto>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());
        // Act
        var act = () => Sut.RemoveNoteAsync(Trip(notes: [Note(NoteId, ViewerId)]), NoteId, CancellationToken.None);
        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        await MockPresenter.DidNotReceive().NotifyAsync(Arg.Any<NotificationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAskForADestructiveConfirmationThenDeleteOnlyTheChosenNote()
    {
        // Arrange
        var other = Note(Guid.NewGuid(), ViewerId, "Other");
        var note = Note(NoteId, ViewerId);
        var trip = Trip(notes: [other, note]);
        // Act
        var removed = await Sut.RemoveNoteAsync(trip, NoteId, CancellationToken.None);
        // Assert
        removed.Should().BeTrue();
        await MockPresenter.Received(1).ShowDialogAsync(
            Arg.Is<DialogRequest>(request => request.Title == Text["TripNoteRemoveTitle"] && request.Message == Text["TripNoteRemoveMessage"]
                && request.Accept == Text["TripNoteRemoveConfirm"] && request.Cancel == Text["Cancel"] && request.IsDestructive),
            Arg.Any<CancellationToken>());
        await MockTrips.Received(1).SaveNoteAsync(trip, note, true, Arg.Any<CancellationToken>());
        await MockTrips.DidNotReceive().SaveNoteAsync(Arg.Any<LocalTripRecordModel>(), other, Arg.Any<bool>(), Arg.Any<CancellationToken>());
        await MockPresenter.DidNotReceive().NotifyAsync(Arg.Any<NotificationRequest>(), Arg.Any<CancellationToken>());
    }
}
