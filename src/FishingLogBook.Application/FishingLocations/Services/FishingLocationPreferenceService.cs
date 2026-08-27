using FishingLogBook.Application.Contracts.Repositories;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Domain.FishingLocations;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using MapsterMapper;

namespace FishingLogBook.Application.FishingLocations.Services;

public sealed class FishingLocationPreferenceService : IFishingLocationPreferenceService
{
    private readonly IFishingLocationPreferenceRepository _fishingLocationPreferenceRepository;
    private readonly IMapper _mapper;

    public FishingLocationPreferenceService(
        IFishingLocationPreferenceRepository fishingLocationPreferenceRepository,
        IMapper mapper)
    {
        _fishingLocationPreferenceRepository = fishingLocationPreferenceRepository;
        _mapper = mapper;
    }

    public async Task<Result<FishingLocationPreferencesDto>> GetPreferencesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var stored = await _fishingLocationPreferenceRepository.GetByUserIdAsync(userId, cancellationToken);
        if (stored.IsFailed)
        {
            return Result.Fail<FishingLocationPreferencesDto>(stored.Errors);
        }

        return Result.Ok(ToPreferences(stored.Value));
    }

    public async Task<Result<FishingLocationPreferencesDto>> UpdatePreferencesAsync(
        Guid userId,
        UpdateFishingLocationPreferencesDto dto,
        CancellationToken cancellationToken)
    {
        var createdOn = DateTimeOffset.UtcNow;
        var locations = dto.Locations
            .Select(location => ToPreference(userId, location, createdOn))
            .ToArray();
        var replaced = await _fishingLocationPreferenceRepository.ReplaceAsync(
            userId,
            locations,
            cancellationToken);
        if (replaced.IsFailed)
        {
            return Result.Fail<FishingLocationPreferencesDto>(replaced.Errors);
        }

        return await GetPreferencesAsync(userId, cancellationToken);
    }

    private static UserFishingLocationPreference ToPreference(
        Guid userId,
        UpdateFishingLocationPreferenceDto dto,
        DateTimeOffset createdOn)
    {
        return new UserFishingLocationPreference
        {
            Id = dto.Id == Guid.Empty ? Guid.NewGuid() : dto.Id,
            UserId = userId,
            Name = FishingLocationConstants.TrimName(dto.Name) ?? string.Empty,
            IsDefault = dto.IsDefault,
            CreatedOn = createdOn
        };
    }

    private FishingLocationPreferencesDto ToPreferences(
        IReadOnlyList<UserFishingLocationPreference> stored)
    {
        var locations = stored
            .OrderByDescending(location => location.IsDefault)
            .ThenBy(location => location.Name, StringComparer.OrdinalIgnoreCase)
            .Select(_mapper.Map<FishingLocationPreferenceDto>)
            .ToArray();
        return new FishingLocationPreferencesDto(locations);
    }
}
