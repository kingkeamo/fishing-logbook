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

namespace FishingLogBook.Application.Catches.Services;

public sealed class CatchService : ICatchService
{
    private static readonly TimeSpan DownloadLifetime = TimeSpan.FromHours(1);

    private readonly ICatchRepository _catchRepository;
    private readonly ICurrentUser _currentUser;
    private readonly ICatchLocationPrivacyService _catchLocationPrivacyService;
    private readonly IObjectStorage _objectStorage;
    private readonly IMapper _mapper;

    public CatchService(
        ICatchRepository catchRepository,
        ICurrentUser currentUser,
        ICatchLocationPrivacyService catchLocationPrivacyService,
        IObjectStorage objectStorage,
        IMapper mapper)
    {
        _catchRepository = catchRepository;
        _currentUser = currentUser;
        _catchLocationPrivacyService = catchLocationPrivacyService;
        _objectStorage = objectStorage;
        _mapper = mapper;
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

        var catchRecord = new Catch
        {
            Id = args.Catch.Id,
            UserId = args.UserId,
            AnglerUserId = args.UserId,
            RecordedByUserId = args.UserId,
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
        var loaded = await LoadForCurrentUserAsync(args.CatchId, cancellationToken);
        if (loaded.IsFailed)
        {
            return Result.Fail<CatchViewDto>(loaded.Errors);
        }

        return Result.Ok(await ToViewDtoAsync(loaded.Value, cancellationToken));
    }

    public async Task<Result<IReadOnlyList<CatchViewDto>>> GetMyAsync(
        GetMyCatchesArgs args,
        CancellationToken cancellationToken)
    {
        var loaded = await _catchRepository.GetByUserIdAsync(args.UserId, cancellationToken);
        if (loaded.IsFailed)
        {
            return Result.Fail<IReadOnlyList<CatchViewDto>>(loaded.Errors);
        }

        var views = new List<CatchViewDto>(loaded.Value.Count);
        foreach (var catchRecord in loaded.Value)
        {
            views.Add(await ToViewDtoAsync(catchRecord, cancellationToken));
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

    private async Task<CatchViewDto> ToViewDtoAsync(Catch catchRecord, CancellationToken cancellationToken)
    {
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
                    catchRecord.UserId,
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
            RecordedByUserId = catchRecord.RecordedByUserId,
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
        Guid userId,
        Guid catchId,
        Guid photographId,
        CancellationToken cancellationToken)
    {
        if (!_objectStorage.IsConfigured)
        {
            return null;
        }

        var objectKey = CatchPhotographObjectKey.Build(userId, catchId, photographId);
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
