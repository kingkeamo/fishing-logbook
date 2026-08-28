using FluentResults;

namespace FishingLogBook.Application.Trips.Errors;

public sealed class TripNoteOutsideTripError : Error
{
    public TripNoteOutsideTripError()
        : base("A trip note must be recorded within the trip.")
    {
    }
}
