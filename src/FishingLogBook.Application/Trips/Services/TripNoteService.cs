using FishingLogBook.Application.Args;
using FishingLogBook.Application.Common.Contracts.Services;
using FishingLogBook.Application.Trips.Contracts.Repositories;
using FishingLogBook.Application.Trips.Contracts.Services;
using FishingLogBook.Application.Trips.Errors;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using MapsterMapper;

namespace FishingLogBook.Application.Trips.Services;

public sealed class TripNoteService : ITripNoteService
{
    private static readonly TimeSpan ClockSkewAllowance = TimeSpan.FromMinutes(5);

    private readonly ITripAccessService _tripAccessService;
    private readonly ITripNoteRepository _tripNoteRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IMapper _mapper;

    public TripNoteService(
        ITripAccessService tripAccessService,
        ITripNoteRepository tripNoteRepository,
        ICurrentUser currentUser,
        IMapper mapper)
    {
        _tripAccessService = tripAccessService;
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

        var access = await _tripAccessService.RequireContributorAsync(args.TripId, cancellationToken);
        if (access.IsFailed)
        {
            return Result.Fail<TripNoteDto>(access.Errors);
        }

        if (!IsWithinTrip(access.Value.Trip, args.RecordedOn))
        {
            return Result.Fail<TripNoteDto>(new TripNoteOutsideTripError());
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

        // Authorship is never inferred from trip ownership, and it is never taken from the client.
        if (existing.Value is not null && existing.Value.CreatedByUserId != _currentUser.UserId)
        {
            return Result.Fail<TripNoteDto>(new TripContributionNotOwnedError());
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
        var access = await _tripAccessService.RequireContributorAsync(args.TripId, cancellationToken);
        if (access.IsFailed)
        {
            return access.ToResult();
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

        if (note.Value.CreatedByUserId != _currentUser.UserId)
        {
            return Result.Fail(new TripContributionNotOwnedError());
        }

        return await _tripNoteRepository.DeleteAsync(args.NoteId, cancellationToken);
    }

    private static bool IsWithinTrip(Trip trip, DateTimeOffset recordedOn)
    {
        if (recordedOn < trip.StartedOn)
        {
            return false;
        }

        return recordedOn <= (trip.EndedOn ?? DateTimeOffset.UtcNow.Add(ClockSkewAllowance));
    }
}
