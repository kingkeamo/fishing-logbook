using FishingLogBook.Application.Args;
using FishingLogBook.Application.Contracts.Repositories;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Application.Trips.Errors;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using MapsterMapper;

namespace FishingLogBook.Application.Trips.Services;

public sealed class TripNoteService : ITripNoteService
{
    private readonly ITripRepository _tripRepository;
    private readonly ITripNoteRepository _tripNoteRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    public TripNoteService(
        ITripRepository tripRepository,
        ITripNoteRepository tripNoteRepository,
        ICurrentUser currentUser,
        IMapper mapper)
    {
        _tripRepository = tripRepository;
        _tripNoteRepository = tripNoteRepository;
        _currentUser = currentUser;
        _mapper = mapper;
    }

    public async Task<Result<TripNoteDto>> RecordAsync(
        RecordTripNoteArgs args,
        CancellationToken cancellationToken)
    {
        var text = TripConstants.TrimNoteText(args.Text);
        if (text is null || !TripConstants.IsNoteTextValid(text))
        {
            return Result.Fail<TripNoteDto>(new TripNoteInvalidError());
        }

        var trip = await LoadOwnedTripAsync(args.TripId, cancellationToken);
        if (trip.IsFailed)
        {
            return Result.Fail<TripNoteDto>(trip.Errors);
        }

        var existing = await _tripNoteRepository.GetByIdAsync(args.NoteId, cancellationToken);
        if (existing.IsFailed)
        {
            return Result.Fail<TripNoteDto>(existing.Errors);
        }

        if (existing.Value is not null && existing.Value.TripId != args.TripId)
        {
            return Result.Fail<TripNoteDto>(new TripNoteNotFoundError());
        }

        var saved = await _tripNoteRepository.UpsertAsync(
            new TripNote
            {
                Id = args.NoteId,
                TripId = args.TripId,
                CreatedByUserId = _currentUser.UserId,
                Text = text,
                RecordedOn = args.RecordedOn
            },
            cancellationToken);
        return saved.IsFailed
            ? Result.Fail<TripNoteDto>(saved.Errors)
            : Result.Ok(_mapper.Map<TripNoteDto>(saved.Value));
    }

    public async Task<Result> DeleteAsync(
        DeleteTripNoteArgs args,
        CancellationToken cancellationToken)
    {
        var trip = await LoadOwnedTripAsync(args.TripId, cancellationToken);
        if (trip.IsFailed)
        {
            return trip.ToResult();
        }

        var note = await _tripNoteRepository.GetByIdAsync(args.NoteId, cancellationToken);
        if (note.IsFailed)
        {
            return note.ToResult();
        }

        if (note.Value is null || note.Value.TripId != args.TripId)
        {
            return Result.Fail(new TripNoteNotFoundError());
        }

        return await _tripNoteRepository.DeleteAsync(args.NoteId, cancellationToken);
    }

    private async Task<Result<Trip>> LoadOwnedTripAsync(Guid tripId, CancellationToken cancellationToken)
    {
        var trip = await _tripRepository.GetByIdAsync(tripId, cancellationToken);
        if (trip.IsFailed)
        {
            return Result.Fail<Trip>(trip.Errors);
        }

        if (trip.Value is null || trip.Value.OwnerUserId != _currentUser.UserId)
        {
            return Result.Fail<Trip>(new TripNoteNotFoundError());
        }

        return Result.Ok(trip.Value);
    }
}
