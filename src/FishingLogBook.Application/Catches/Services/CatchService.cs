using FishingLogBook.Application.Args;
using FishingLogBook.Application.Capabilities.Errors;
using FishingLogBook.Application.Catches.Errors;
using FishingLogBook.Application.Contracts;
using FishingLogBook.Application.Contracts.Repositories;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Domain.Catches;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using MapsterMapper;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Application.Catches.Services;

public sealed class CatchService : ICatchService
{
    private static readonly TimeSpan DownloadLifetime = TimeSpan.FromHours(1);

    private readonly ICatchRepository _catchRepository;
    private readonly ITripAccessService _tripAccessService;
    private readonly ICurrentUser _currentUser;
    private readonly ICatchLocationPrivacyService _catchLocationPrivacyService;
    private readonly IObjectStorage _objectStorage;
    private readonly IMapper _mapper;
    private readonly ILogger<CatchService> _logger;

    public CatchService(
        ICatchRepository catchRepository,
        ITripAccessService tripAccessService,
        ICurrentUser currentUser,
        ICatchLocationPrivacyService catchLocationPrivacyService,
        IObjectStorage objectStorage,
        IMapper mapper,
        ILogger<CatchService> logger)
    {
        _catchRepository = catchRepository;
        _tripAccessService = tripAccessService;
        _currentUser = currentUser;
        _catchLocationPrivacyService = catchLocationPrivacyService;
        _objectStorage = objectStorage;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<Result<CatchDto>> UpsertAsync(UpsertCatchArgs args, CancellationToken cancellationToken)
    {
        var photographs = args.Catch.Photographs ?? [];
        if (photographs.Count == 0)
        {
            return Result.Fail<CatchDto>(new CatchHasNoPhotographsError());
        }

        if (photographs.Any(photograph =>
                photograph.Id == Guid.Empty ||
                photograph.CatchId != args.Catch.Id))
        {
            return Result.Fail<CatchDto>(new CatchPhotographIdentityError());
        }

        var location = ToLocation(args.Catch.Location);
        if (args.Catch.Location is not null && location is null)
        {
            return Result.Fail<CatchDto>(new CatchLocationInvalidError());
        }

        var details = ValidateDetails(args.Catch);
        if (details.IsFailed)
        {
            return Result.Fail<CatchDto>(details.Errors);
        }

        var trip = await ResolveTripAsync(args.Catch.TripId, args.UserId, cancellationToken);
        if (trip.IsFailed)
        {
            return Result.Fail<CatchDto>(trip.Errors);
        }

        var existingResult = await _catchRepository.GetByIdAsync(args.Catch.Id, cancellationToken);
        if (existingResult.IsFailed)
        {
            return Result.Fail<CatchDto>(existingResult.Errors);
        }

        var existing = existingResult.Value;
        if (existing is not null
            && existing.AnglerUserId != args.UserId
            && existing.RecordedByUserId != args.UserId)
        {
            return Result.Fail<CatchDto>(new CatchEditNotPermittedError());
        }

        var identity = existing is not null
            ? Result.Ok((UserId: existing.UserId, AnglerUserId: existing.AnglerUserId))
            : await ResolveAnglerAsync(args, trip.Value, cancellationToken);
        if (identity.IsFailed)
        {
            return Result.Fail<CatchDto>(identity.Errors);
        }

        var catchRecord = new Catch
        {
            Id = args.Catch.Id,
            UserId = identity.Value.UserId,
            AnglerUserId = identity.Value.AnglerUserId,
            RecordedByUserId = existing?.RecordedByUserId ?? args.UserId,
            TripId = trip.Value,
            CaughtOn = args.Catch.CaughtOn,
            SpeciesName = TrimToNull(args.Catch.SpeciesName),
            Weight = args.Catch.Weight,
            Length = args.Catch.Length,
            Method = TrimToNull(args.Catch.Method),
            BaitOrLure = TrimToNull(args.Catch.BaitOrLure),
            Notes = TrimToNull(args.Catch.Notes),
            Location = location,
            Photographs = photographs
                .Select(photograph => new CatchPhotograph
                {
                    Id = photograph.Id,
                    CatchId = args.Catch.Id,
                    ContentType = photograph.ContentType
                })
                .ToArray()
        };

        var saved = await _catchRepository.UpsertAsync(catchRecord, cancellationToken);
        if (saved.IsFailed)
        {
            return Result.Fail<CatchDto>(saved.Errors);
        }

        return Result.Ok(_mapper.Map<CatchDto>(saved.Value));
    }

    public async Task<Result<CatchViewDto>> GetViewAsync(GetCatchArgs args, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsResolved)
        {
            return Result.Fail<CatchViewDto>(new CurrentUserUnresolvedError());
        }

        var loaded = await _catchRepository.GetDetailForUserAsync(
            args.CatchId,
            _currentUser.UserId,
            cancellationToken);
        if (loaded.IsFailed)
        {
            return Result.Fail<CatchViewDto>(loaded.Errors);
        }

        if (loaded.Value is null)
        {
            return Result.Fail<CatchViewDto>(new CatchNotFoundError());
        }

        return Result.Ok(await ToViewDtoAsync(loaded.Value, cancellationToken));
    }

    public async Task<Result<IReadOnlyList<CatchViewDto>>> GetMyAsync(
        GetMyCatchesArgs args,
        CancellationToken cancellationToken)
    {
        var loaded = await _catchRepository.GetActivityForUserAsync(args.UserId, cancellationToken);
        if (loaded.IsFailed)
        {
            return Result.Fail<IReadOnlyList<CatchViewDto>>(loaded.Errors);
        }

        var views = new List<CatchViewDto>(loaded.Value.Count);
        foreach (var catchDetail in loaded.Value)
        {
            views.Add(await ToViewDtoAsync(catchDetail, cancellationToken));
        }

        return Result.Ok<IReadOnlyList<CatchViewDto>>(views);
    }

    public async Task<Result> UpdateLocationVisibilityAsync(
        UpdateCatchLocationVisibilityArgs args,
        CancellationToken cancellationToken)
    {
        var loaded = await LoadForCurrentUserAsync(args.CatchId, cancellationToken);
        if (loaded.IsFailed)
        {
            return loaded.ToResult();
        }

        if (loaded.Value.UserId != _currentUser.UserId)
        {
            return Result.Fail(new CatchNotOwnedError());
        }

        if (loaded.Value.Location is null)
        {
            return Result.Fail(new CatchHasNoLocationError());
        }

        return await _catchRepository.UpdateLocationVisibilityAsync(
            new PersistCatchLocationVisibilityArgs
            {
                CatchId = args.CatchId,
                UserId = _currentUser.UserId,
                Visibility = args.Visibility
            },
            cancellationToken);
    }

    public async Task<Result<CatchViewDto>> CorrectAnglerAsync(
        CorrectCatchAnglerArgs args,
        CancellationToken cancellationToken)
    {
        var loaded = await LoadForCurrentUserAsync(args.CatchId, cancellationToken);
        if (loaded.IsFailed)
        {
            return Result.Fail<CatchViewDto>(loaded.Errors);
        }

        var existing = loaded.Value;
        if (existing.AnglerUserId != _currentUser.UserId && existing.RecordedByUserId != _currentUser.UserId)
        {
            return Result.Fail<CatchViewDto>(new CatchEditNotPermittedError());
        }

        if (existing.TripId is not { } tripId)
        {
            return Result.Fail<CatchViewDto>(new CatchNotOnTripError());
        }

        if (args.AnglerUserId != existing.AnglerUserId)
        {
            var anglerAccess = await _tripAccessService.ResolveForAsync(tripId, args.AnglerUserId, cancellationToken);
            if (anglerAccess.IsFailed || !anglerAccess.Value.CanContribute)
            {
                return Result.Fail<CatchViewDto>(new CatchAnglerNotEligibleError());
            }

            var corrected = await _catchRepository.CorrectAnglerAsync(
                new PersistCatchAnglerArgs
                {
                    CatchId = args.CatchId,
                    AnglerUserId = args.AnglerUserId
                },
                cancellationToken);
            if (corrected.IsFailed)
            {
                return Result.Fail<CatchViewDto>(corrected.Errors);
            }
        }

        var refreshed = await _catchRepository.GetDetailForUserAsync(
            args.CatchId,
            _currentUser.UserId,
            cancellationToken);
        if (refreshed.IsFailed)
        {
            return Result.Fail<CatchViewDto>(refreshed.Errors);
        }

        if (refreshed.Value is null)
        {
            return Result.Fail<CatchViewDto>(new CatchNotFoundError());
        }

        return Result.Ok(await ToViewDtoAsync(refreshed.Value, cancellationToken));
    }

    private async Task<CatchViewDto> ToViewDtoAsync(CatchDetail catchDetail, CancellationToken cancellationToken)
    {
        var catchRecord = catchDetail.Catch;
        var exposure = await _catchLocationPrivacyService.GetExposureAsync(
            catchRecord,
            _currentUser.UserId,
            cancellationToken);

        var photographs = new List<CatchPhotographViewDto>(catchRecord.Photographs.Count);
        foreach (var photograph in catchRecord.Photographs)
        {
            photographs.Add(new CatchPhotographViewDto(
                photograph.Id,
                photograph.ContentType,
                await CreatePhotographUrlAsync(
                    catchRecord.Id,
                    photograph.Id,
                    cancellationToken)));
        }

        return new CatchViewDto(
            catchRecord.Id,
            catchRecord.UserId,
            catchRecord.CaughtOn,
            exposure)
        {
            AnglerUserId = catchRecord.AnglerUserId,
            AnglerName = catchDetail.AnglerName,
            RecordedByUserId = catchRecord.RecordedByUserId,
            RecordedByName = catchDetail.RecordedByName,
            TripId = catchRecord.TripId,
            SpeciesName = catchRecord.SpeciesName,
            Weight = catchRecord.Weight,
            Length = catchRecord.Length,
            Method = catchRecord.Method,
            BaitOrLure = catchRecord.BaitOrLure,
            Notes = catchRecord.Notes,
            Photographs = photographs
        };
    }

    private async Task<string?> CreatePhotographUrlAsync(
        Guid catchId,
        Guid photographId,
        CancellationToken cancellationToken)
    {
        if (!_objectStorage.IsConfigured)
        {
            return null;
        }

        var objectKey = CatchPhotographObjectKey.Build(catchId, photographId);
        var url = await _objectStorage.CreateDownloadUrlAsync(objectKey, DownloadLifetime, cancellationToken);
        return url.ToString();
    }

    private async Task<Result<Catch>> LoadForCurrentUserAsync(Guid catchId, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsResolved)
        {
            return Result.Fail<Catch>(new CurrentUserUnresolvedError());
        }

        var loaded = await _catchRepository.GetByIdAsync(catchId, cancellationToken);
        if (loaded.IsFailed)
        {
            return Result.Fail<Catch>(loaded.Errors);
        }

        if (loaded.Value is null)
        {
            return Result.Fail<Catch>(new CatchNotFoundError());
        }

        return Result.Ok(loaded.Value);
    }

    private static Result ValidateDetails(CatchDto catchDto)
    {
        if (!CatchDetailConstants.IsCaughtOnValid(catchDto.CaughtOn, DateTimeOffset.UtcNow)
            || !CatchDetailConstants.IsWeightValid(catchDto.Weight)
            || !CatchDetailConstants.IsLengthValid(catchDto.Length)
            || !CatchDetailConstants.IsOptionalTextValid(
                catchDto.SpeciesName,
                CatchDetailConstants.MaxSpeciesNameLength)
            || !CatchDetailConstants.IsOptionalTextValid(
                catchDto.Method,
                CatchDetailConstants.MaxMethodLength)
            || !CatchDetailConstants.IsOptionalTextValid(
                catchDto.BaitOrLure,
                CatchDetailConstants.MaxBaitOrLureLength)
            || !CatchDetailConstants.IsOptionalTextValid(
                catchDto.Notes,
                CatchDetailConstants.MaxNotesLength))
        {
            return Result.Fail(new CatchDetailsInvalidError());
        }

        return Result.Ok();
    }

    private async Task<Result<Guid?>> ResolveTripAsync(
        Guid? tripId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (tripId is null || tripId == Guid.Empty)
        {
            return Result.Ok<Guid?>(null);
        }

        var access = await _tripAccessService.ResolveForAsync(tripId.Value, userId, cancellationToken);
        if (access.IsFailed)
        {
            return Result.Fail<Guid?>(new CatchTripInvalidError());
        }

        if (!access.Value.CanContribute)
        {
            return Result.Fail<Guid?>(new CatchTripInvalidError());
        }

        return Result.Ok<Guid?>(tripId.Value);
    }

    private async Task<Result<(Guid UserId, Guid AnglerUserId)>> ResolveAnglerAsync(
        UpsertCatchArgs args,
        Guid? tripId,
        CancellationToken cancellationToken)
    {
        var requestedAngler = args.Catch.AnglerUserId == Guid.Empty
            ? args.UserId
            : args.Catch.AnglerUserId;

        if (requestedAngler == args.UserId)
        {
            return Result.Ok((UserId: args.UserId, AnglerUserId: args.UserId));
        }

        if (tripId is null)
        {
            return Result.Fail<(Guid, Guid)>(new CatchAnglerNotEligibleError());
        }

        var anglerAccess = await _tripAccessService.ResolveForAsync(tripId.Value, requestedAngler, cancellationToken);
        if (anglerAccess.IsFailed || !anglerAccess.Value.CanContribute)
        {
            return Result.Fail<(Guid, Guid)>(new CatchAnglerNotEligibleError());
        }

        return Result.Ok((UserId: requestedAngler, AnglerUserId: requestedAngler));
    }

    private static string? TrimToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static CatchLocation? ToLocation(CatchLocationDto? location)
    {
        if (location is null)
        {
            return null;
        }

        return CatchLocation.TryCreate(
            location.Latitude,
            location.Longitude,
            location.AccuracyMetres,
            location.CapturedOn,
            location.Source,
            location.Visibility,
            location.ConsentVersion);
    }
}
