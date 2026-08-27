using FluentResults;

namespace FishingLogBook.Application.Trips.Errors;

public sealed class TripNoteNotFoundError : Error
{
    public TripNoteNotFoundError()
        : base("The trip note could not be found.")
    {
    }
}
