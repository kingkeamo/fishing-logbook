using FishingLogBook.Application.Args;
using FishingLogBook.Shared.Dtos;
using FluentResults;

namespace FishingLogBook.Application.Trips.Contracts.Services;

public interface ITripNoteService
{
    Task<Result<TripNoteDto>> RecordAsync(RecordTripNoteArgs args, CancellationToken cancellationToken);

    Task<Result> DeleteAsync(DeleteTripNoteArgs args, CancellationToken cancellationToken);
}
