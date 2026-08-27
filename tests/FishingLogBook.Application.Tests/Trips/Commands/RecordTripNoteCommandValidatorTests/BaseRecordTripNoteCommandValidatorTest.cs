using FishingLogBook.Application.Trips.Commands;

namespace FishingLogBook.Application.Tests.Trips.Commands.RecordTripNoteCommandValidatorTests;

public class BaseRecordTripNoteCommandValidatorTest
{
    protected static readonly Guid TripId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    protected static readonly Guid NoteId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    protected static readonly DateTimeOffset RecordedOn = DateTimeOffset.Parse("2026-08-17T09:12:00Z");

    protected readonly RecordTripNoteCommandValidator Sut = new();
}
