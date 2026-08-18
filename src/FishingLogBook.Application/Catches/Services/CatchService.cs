using FishingLogBook.Application.Args;
using FishingLogBook.Application.Capabilities.Errors;
using FishingLogBook.Application.Catches.Errors;
using FishingLogBook.Application.Contracts.Repositories;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Domain.Catches;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using Mapster;

namespace FishingLogBook.Application.Catches.Services;

public sealed class CatchService : ICatchService
{
    private readonly ICatchRepository _catchRepository;
    private readonly ICurrentUser _currentUser;
    private readonly ICatchLocationPrivacyService _catchLocationPrivacyService;

    public CatchService(
        ICatchRepository catchRepository,
        ICurrentUser currentUser,
        ICatchLocationPrivacyService catchLocationPrivacyService)
    {
        _catchRepository = catchRepository;
        _currentUser = currentUser;
        _catchLocationPrivacyService = catchLocationPrivacyService;
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

        return Result.Ok(saved.Value.Adapt<CatchDto>());
    }

    public async Task<Result<CatchViewDto>> GetViewAsync(GetCatchArgs args, CancellationToken cancellationToken)
    {
        var loaded = await LoadForCurrentUserAsync(args.CatchId, cancellationToken);
        if (loaded.IsFailed)
        {
            return Result.Fail<CatchViewDto>(loaded.Errors);
        }

        var exposure = await _catchLocationPrivacyService.GetExposureAsync(
            loaded.Value,
            _currentUser.UserId,
            cancellationToken);
        return Result.Ok(new CatchViewDto(
            loaded.Value.Id,
            loaded.Value.UserId,
            loaded.Value.CaughtOn,
            exposure)
        {
            AnglerUserId = loaded.Value.AnglerUserId,
            RecordedByUserId = loaded.Value.RecordedByUserId,
            SpeciesName = loaded.Value.SpeciesName,
            Weight = loaded.Value.Weight,
            Length = loaded.Value.Length,
            Method = loaded.Value.Method,
            BaitOrLure = loaded.Value.BaitOrLure,
            Notes = loaded.Value.Notes
        });
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
