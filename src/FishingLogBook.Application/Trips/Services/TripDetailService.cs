using FishingLogBook.Application.Args;
using FishingLogBook.Application.Capabilities.Errors;
using FishingLogBook.Application.Catches;
using FishingLogBook.Application.Contracts;
using FishingLogBook.Application.Contracts.Repositories;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Application.Trips.Errors;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using MapsterMapper;

namespace FishingLogBook.Application.Trips.Services;

public sealed class TripDetailService : ITripDetailService
{
    private static readonly TimeSpan DownloadLifetime = TimeSpan.FromMinutes(15);

    private readonly ITripRepository _tripRepository;
    private readonly ITripNoteRepository _tripNoteRepository;
    private readonly ITripPhotographRepository _tripPhotographRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IObjectStorage _objectStorage;
    private readonly IMapper _mapper;

    public TripDetailService(
        ITripRepository tripRepository,
        ITripNoteRepository tripNoteRepository,
        ITripPhotographRepository tripPhotographRepository,
        ICurrentUser currentUser,
        IObjectStorage objectStorage,
        IMapper mapper)
    {
        _tripRepository = tripRepository;
        _tripNoteRepository = tripNoteRepository;
        _tripPhotographRepository = tripPhotographRepository;
        _currentUser = currentUser;
        _objectStorage = objectStorage;
        _mapper = mapper;
    }

    public async Task<Result<TripDetailDto>> GetAsync(GetTripArgs args, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsResolved)
        {
            return Result.Fail<TripDetailDto>(new CurrentUserUnresolvedError());
        }

        var loaded = await _tripRepository.GetByIdAsync(args.TripId, cancellationToken);
        if (loaded.IsFailed)
        {
            return Result.Fail<TripDetailDto>(loaded.Errors);
        }

        if (loaded.Value is null || loaded.Value.OwnerUserId != _currentUser.UserId)
        {
            return Result.Fail<TripDetailDto>(new TripNotFoundError());
        }

        var notes = await _tripNoteRepository.GetByTripIdAsync(args.TripId, cancellationToken);
        if (notes.IsFailed)
        {
            return Result.Fail<TripDetailDto>(notes.Errors);
        }

        var photographs = await _tripPhotographRepository.GetByTripIdAsync(args.TripId, cancellationToken);
        if (photographs.IsFailed)
        {
            return Result.Fail<TripDetailDto>(photographs.Errors);
        }

        var catches = await _tripRepository.GetCatchSummariesByTripIdAsync(args.TripId, cancellationToken);
        if (catches.IsFailed)
        {
            return Result.Fail<TripDetailDto>(catches.Errors);
        }

        return Result.Ok(new TripDetailDto(_mapper.Map<TripViewDto>(loaded.Value))
        {
            Notes = [.. notes.Value.OrderBy(note => note.RecordedOn).Select(_mapper.Map<TripNoteDto>)],
            Photographs = await ToViewsAsync(photographs.Value, cancellationToken),
            Catches = await ToCatchSummariesAsync(catches.Value, cancellationToken)
        });
    }

    private async Task<IReadOnlyList<TripCatchSummaryDto>> ToCatchSummariesAsync(
        IReadOnlyList<TripCatchSummary> catches,
        CancellationToken cancellationToken)
    {
        var summaries = new List<TripCatchSummaryDto>(catches.Count);
        foreach (var summary in catches)
        {
            summaries.Add(_mapper.Map<TripCatchSummaryDto>(summary) with
            {
                PhotographUrl = await CreateCatchPhotographUrlAsync(summary, cancellationToken)
            });
        }

        return summaries;
    }

    private async Task<string?> CreateCatchPhotographUrlAsync(
        TripCatchSummary summary,
        CancellationToken cancellationToken)
    {
        if (summary.PhotographId is not { } photographId)
        {
            return null;
        }

        return await CreateDownloadUrlAsync(
            CatchPhotographObjectKey.Build(summary.UserId, summary.Id, photographId),
            cancellationToken);
    }

    private async Task<IReadOnlyList<TripPhotographViewDto>> ToViewsAsync(
        IReadOnlyList<TripPhotograph> photographs,
        CancellationToken cancellationToken)
    {
        var views = new List<TripPhotographViewDto>(photographs.Count);
        foreach (var photograph in photographs.OrderBy(photograph => photograph.AddedOn))
        {
            views.Add(new TripPhotographViewDto(
                photograph.Id,
                photograph.ContentType,
                photograph.AddedOn,
                await CreateDownloadUrlAsync(photograph.ObjectKey, cancellationToken),
                photograph.CapturedOn));
        }

        return views;
    }

    private async Task<string?> CreateDownloadUrlAsync(string? objectKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(objectKey) || !_objectStorage.IsConfigured)
        {
            return null;
        }

        var url = await _objectStorage.CreateDownloadUrlAsync(objectKey, DownloadLifetime, cancellationToken);
        return url.ToString();
    }
}
