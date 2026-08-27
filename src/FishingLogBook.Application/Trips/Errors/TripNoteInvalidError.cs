using FluentResults;

namespace FishingLogBook.Application.Trips.Errors;

public sealed class TripNoteInvalidError : Error
{
    public TripNoteInvalidError()
        : base("A trip note needs some text.")
    {
    }
}
