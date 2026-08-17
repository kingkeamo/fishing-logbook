using FishingLogBook.Application.Args;
using FishingLogBook.Application.Catches.Errors;
using FishingLogBook.Application.Contracts.Repositories;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Domain.Catches;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using Mapster;

namespace FishingLogBook.Application.Catches.Services;

public sealed class CatchService : ICatchService
{
    private readonly ICatchRepository _catchRepository;

    public CatchService(ICatchRepository catchRepository)
    {
        _catchRepository = catchRepository;
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

        var catchRecord = new Catch
        {
            Id = args.Catch.Id,
            UserId = args.UserId,
            CaughtOn = args.Catch.CaughtOn,
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
