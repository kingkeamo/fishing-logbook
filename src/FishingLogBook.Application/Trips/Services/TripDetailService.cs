using FishingLogBook.Application.Args;
using FishingLogBook.Application.Catches.Contracts.Builders;
using FishingLogBook.Application.Catches.Contracts.Services;
using FishingLogBook.Application.Common.Contracts.Services;
using FishingLogBook.Application.Profiles.Contracts.Services;
using FishingLogBook.Application.Trips.Contracts.Repositories;
using FishingLogBook.Application.Trips.Contracts.Services;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using MapsterMapper;

namespace FishingLogBook.Application.Trips.Services;

public sealed class TripDetailService : ITripDetailService
{
    private static readonly TimeSpan DownloadLifetime = TimeSpan.FromMinutes(15);

    private readonly ITripAccessService _tripAccessService;
    private readonly ITripNoteRepository _tripNoteRepository;
    private readonly ITripPhotographRepository _tripPhotographRepository;
    private readonly ITripRepository _tripRepository;
    private readonly IAnglerLookupService _anglerLookupService;
    private readonly IObjectStorage _objectStorage;
    private readonly ICatchPhotographObjectKeyBuilder _catchObjectKeyBuilder;
    private readonly IMapper _mapper;

    public TripDetailService(
        ITripAccessService tripAccessService,
        ITripNoteRepository tripNoteRepository,
        ITripPhotographRepository tripPhotographRepository,
        ITripRepository tripRepository,
        IAnglerLookupService anglerLookupService,
        IObjectStorage objectStorage,
        ICatchPhotographObjectKeyBuilder catchObjectKeyBuilder,
        IMapper mapper)
    {
        _tripAccessService = tripAccessService;
        _tripNoteRepository = tripNoteRepository;
        _tripPhotographRepository = tripPhotographRepository;
        _tripRepository = tripRepository;
        _anglerLookupService = anglerLookupService;
        _objectStorage = objectStorage;
        _catchObjectKeyBuilder = catchObjectKeyBuilder;
        _mapper = mapper;
    }

    public async Task<Result<TripDetailDto>> GetAsync(GetTripArgs args, CancellationToken cancellationToken)
    {
        var access = await _tripAccessService.RequireContributorAsync(args.TripId, cancellationToken);
        if (access.IsFailed)
        {
            return Result.Fail<TripDetailDto>(access.Errors);
        }

        var trip = access.Value.Trip;
        var content = await LoadContentAsync(args.TripId, cancellationToken);
        if (content.IsFailed)
        {
            return Result.Fail<TripDetailDto>(content.Errors);
        }

        var contributors = await DescribeContributorsAsync(trip, content.Value, cancellationToken);
        if (contributors.IsFailed)
        {
            return Result.Fail<TripDetailDto>(contributors.Errors);
        }

        return Result.Ok(new TripDetailDto(_mapper.Map<TripViewDto>(trip))
        {
            Role = ToRole(access.Value.Role),
            Notes = [.. content.Value.Notes.OrderBy(note => note.RecordedOn).Select(_mapper.Map<TripNoteDto>)],
            Photographs = await ToViewsAsync(content.Value.Photographs, cancellationToken),
            Catches = await ToCatchSummariesAsync(content.Value.Catches, cancellationToken),
            Contributors = contributors.Value
        });
    }

    private async Task<Result<TripContent>> LoadContentAsync(Guid tripId, CancellationToken cancellationToken)
    {
        var notes = await _tripNoteRepository.GetByTripIdAsync(tripId, cancellationToken);
        if (notes.IsFailed)
        {
            return Result.Fail<TripContent>(notes.Errors);
        }

        var photographs = await _tripPhotographRepository.GetByTripIdAsync(tripId, cancellationToken);
        if (photographs.IsFailed)
        {
            return Result.Fail<TripContent>(photographs.Errors);
        }

        var catches = await _tripRepository.GetCatchSummariesByTripIdAsync(tripId, cancellationToken);
        if (catches.IsFailed)
        {
            return Result.Fail<TripContent>(catches.Errors);
        }

        return Result.Ok(new TripContent(notes.Value, photographs.Value, catches.Value));
    }

    private async Task<Result<IReadOnlyList<TripContributorDto>>> DescribeContributorsAsync(
        Trip trip,
        TripContent content,
        CancellationToken cancellationToken)
    {
        var userIds = ContributorUserIds(trip, content);
        var described = await _anglerLookupService.DescribeAsync(userIds, cancellationToken);
        if (described.IsFailed)
        {
            return Result.Fail<IReadOnlyList<TripContributorDto>>(described.Errors);
        }

        IReadOnlyList<TripContributorDto> contributors =
        [
            .. userIds.Select(userId => ToContributor(userId, trip.OwnerUserId, described.Value))
        ];
        return Result.Ok(contributors);
    }

    private static IReadOnlyList<Guid> ContributorUserIds(Trip trip, TripContent content)
    {
        return
        [
            .. content.Notes
                .Select(note => note.CreatedByUserId)
                .Concat(content.Photographs.Select(photograph => photograph.ContributedByUserId))
                .Concat(content.Catches.Select(summary => summary.AnglerUserId))
                .Concat(content.Catches.Select(summary => summary.RecordedByUserId))
                .Append(trip.OwnerUserId)
                .Where(userId => userId != Guid.Empty)
                .Distinct()
        ];
    }

    private static TripContributorDto ToContributor(
        Guid userId,
        Guid ownerUserId,
        IReadOnlyDictionary<Guid, AnglerSummaryDto> described)
    {
        var angler = described.GetValueOrDefault(userId);
        return new TripContributorDto(userId, angler?.DisplayName, angler?.PhotographUrl)
        {
            IsOwner = userId == ownerUserId
        };
    }

    private static string ToRole(TripAccessRoleEnum role)
    {
        return role switch
        {
            TripAccessRoleEnum.Owner => TripParticipantConstants.Owner,
            TripAccessRoleEnum.Participant => TripParticipantConstants.Participant,
            _ => TripParticipantConstants.None
        };
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
            _catchObjectKeyBuilder.Build(summary.Id, photographId),
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
                photograph.CapturedOn)
            {
                ContributedByUserId = photograph.ContributedByUserId
            });
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

    private sealed record TripContent(
        IReadOnlyList<TripNote> Notes,
        IReadOnlyList<TripPhotograph> Photographs,
        IReadOnlyList<TripCatchSummary> Catches);
}
