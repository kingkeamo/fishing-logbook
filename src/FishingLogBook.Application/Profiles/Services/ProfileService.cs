using FishingLogBook.Application.Args;
using FishingLogBook.Application.Contracts;
using FishingLogBook.Application.Contracts.Repositories;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Application.Profiles.Errors;
using FishingLogBook.Domain.Profiles;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using FluentResults;

namespace FishingLogBook.Application.Profiles.Services;

public sealed class ProfileService : IProfileService
{
    private static readonly TimeSpan DownloadLifetime = TimeSpan.FromHours(1);
    private static readonly TimeSpan UploadLifetime = TimeSpan.FromMinutes(15);

    private readonly IProfileRepository _profileRepository;
    private readonly IObjectStorage _objectStorage;
    private readonly IFishingPreferenceService _fishingPreferenceService;

    public ProfileService(
        IProfileRepository profileRepository,
        IObjectStorage objectStorage,
        IFishingPreferenceService fishingPreferenceService)
    {
        _profileRepository = profileRepository;
        _objectStorage = objectStorage;
        _fishingPreferenceService = fishingPreferenceService;
    }

    public bool IsObjectStorageConfigured => _objectStorage.IsConfigured;

    public async Task<Result<ProfileDto>> GetOwnAsync(Guid userId, CancellationToken cancellationToken)
    {
        var existing = await _profileRepository.GetByUserIdAsync(userId, cancellationToken);
        if (existing.IsFailed)
        {
            return Result.Fail<ProfileDto>(existing.Errors);
        }

        var profile = existing.Value ?? CreateDefault(userId);
        return Result.Ok(await ToOwnDtoAsync(profile, cancellationToken));
    }

    public async Task<Result<ProfileDto>> UpdateOwnAsync(UpdateProfileArgs args, CancellationToken cancellationToken)
    {
        var existing = await _profileRepository.GetByUserIdAsync(args.UserId, cancellationToken);
        if (existing.IsFailed)
        {
            return Result.Fail<ProfileDto>(existing.Errors);
        }

        var current = existing.Value ?? CreateDefault(args.UserId);
        var updated = await _profileRepository.UpsertAsync(ApplyUpdate(current, args), cancellationToken);
        if (updated.IsFailed)
        {
            return Result.Fail<ProfileDto>(updated.Errors);
        }

        return Result.Ok(await ToOwnDtoAsync(updated.Value, cancellationToken));
    }

    public async Task<Result<ProfileDto>> CompleteOnboardingAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var completed = await _profileRepository.CompleteOnboardingAsync(userId, cancellationToken);
        if (completed.IsFailed)
        {
            return Result.Fail<ProfileDto>(completed.Errors);
        }

        return Result.Ok(await ToOwnDtoAsync(completed.Value, cancellationToken));
    }

    public async Task<Result<PublicProfileDto>> GetPublicAsync(Guid userId, CancellationToken cancellationToken)
    {
        var exists = await _profileRepository.UserExistsAsync(userId, cancellationToken);
        if (exists.IsFailed)
        {
            return Result.Fail<PublicProfileDto>(exists.Errors);
        }

        if (!exists.Value)
        {
            return Result.Fail<PublicProfileDto>(new ProfileNotFoundError());
        }

        var existing = await _profileRepository.GetByUserIdAsync(userId, cancellationToken);
        if (existing.IsFailed)
        {
            return Result.Fail<PublicProfileDto>(existing.Errors);
        }

        var profile = existing.Value ?? CreateDefault(userId);
        return await ToPublicDtoAsync(profile, cancellationToken);
    }

    public async Task<Result<PhotographUploadDto>> CreatePhotographUploadAsync(
        Guid userId,
        PhotographUploadRequestDto request,
        CancellationToken cancellationToken)
    {
        var ensured = await EnsureOwnProfileAsync(userId, cancellationToken);
        if (ensured.IsFailed)
        {
            return Result.Fail<PhotographUploadDto>(ensured.Errors);
        }

        var objectKey = ObjectKey(userId, request.PhotographId);
        var uploadUrl = await _objectStorage.CreateUploadUrlAsync(
            objectKey,
            request.ContentType,
            UploadLifetime,
            cancellationToken);
        return Result.Ok(new PhotographUploadDto(objectKey, uploadUrl.ToString()));
    }

    public async Task<Result<ProfileDto>> RecordPhotographAsync(
        RecordProfilePhotographArgs args,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(args.ObjectKey, ObjectKey(args.UserId, args.PhotographId), StringComparison.Ordinal))
        {
            return Result.Fail<ProfileDto>(new PhotographObjectKeyMismatchError());
        }

        var updated = await _profileRepository.UpdatePhotographAsync(args, cancellationToken);
        if (updated.IsFailed)
        {
            return Result.Fail<ProfileDto>(updated.Errors);
        }

        return Result.Ok(await ToOwnDtoAsync(updated.Value, cancellationToken));
    }

    private async Task<Result<Profile>> EnsureOwnProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        var existing = await _profileRepository.GetByUserIdAsync(userId, cancellationToken);
        if (existing.IsFailed)
        {
            return Result.Fail<Profile>(existing.Errors);
        }

        if (existing.Value is Profile profile)
        {
            return Result.Ok(profile);
        }

        return await _profileRepository.UpsertAsync(CreateDefault(userId), cancellationToken);
    }

    private async Task<ProfileDto> ToOwnDtoAsync(Profile profile, CancellationToken cancellationToken)
    {
        return new ProfileDto(
            profile.UserId,
            profile.DisplayName,
            profile.PhotographId,
            await CreateDownloadUrlAsync(profile.PhotographObjectKey, cancellationToken),
            profile.PhotographContentType,
            profile.HomeRegion,
            profile.ShowDisplayName,
            profile.ShowPhotograph,
            profile.ShowHomeRegion,
            profile.ShowPreferredFishingMethods,
            profile.ShowPreferredSpecies,
            (WeightUnitEnum)profile.PreferredWeightUnit,
            (LengthUnitEnum)profile.PreferredLengthUnit,
            profile.OnboardingCompletedOn.HasValue);
    }

    private async Task<Result<PublicProfileDto>> ToPublicDtoAsync(Profile profile, CancellationToken cancellationToken)
    {
        var photographUrl = profile.ShowPhotograph
            ? await CreateDownloadUrlAsync(profile.PhotographObjectKey, cancellationToken)
            : null;

        IReadOnlyList<string> methods = [];
        IReadOnlyList<string> species = [];
        if (profile.ShowPreferredFishingMethods || profile.ShowPreferredSpecies)
        {
            var preferences = await _fishingPreferenceService.GetPreferencesAsync(profile.UserId, cancellationToken);
            if (preferences.IsFailed)
            {
                return Result.Fail<PublicProfileDto>(preferences.Errors);
            }

            if (profile.ShowPreferredFishingMethods)
            {
                methods = [.. preferences.Value.Methods.Select(method => method.Name)];
            }

            if (profile.ShowPreferredSpecies)
            {
                species =
                [
                    .. preferences.Value.Methods
                        .SelectMany(method => method.Species)
                        .Select(preference => preference.Name)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                ];
            }
        }

        return Result.Ok(new PublicProfileDto(
            profile.UserId,
            profile.ShowDisplayName ? profile.DisplayName : null,
            photographUrl,
            profile.ShowHomeRegion ? profile.HomeRegion : null,
            methods,
            species));
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

    private static Profile CreateDefault(Guid userId)
    {
        return new Profile { UserId = userId };
    }

    private static Profile ApplyUpdate(Profile current, UpdateProfileArgs args)
    {
        return new Profile
        {
            UserId = args.UserId,
            DisplayName = TrimOrNull(args.DisplayName),
            PhotographId = current.PhotographId,
            PhotographObjectKey = current.PhotographObjectKey,
            PhotographContentType = current.PhotographContentType,
            HomeRegion = TrimOrNull(args.HomeRegion),
            PreferredWeightUnit = args.PreferredWeightUnit,
            PreferredLengthUnit = args.PreferredLengthUnit,
            ShowDisplayName = args.ShowDisplayName,
            ShowPhotograph = args.ShowPhotograph,
            ShowHomeRegion = args.ShowHomeRegion,
            ShowPreferredFishingMethods = args.ShowPreferredFishingMethods,
            ShowPreferredSpecies = args.ShowPreferredSpecies,
            OnboardingCompletedOn = current.OnboardingCompletedOn
        };
    }

    private static string ObjectKey(Guid userId, Guid photographId)
    {
        return $"profiles/{userId:D}/{photographId:D}";
    }

    private static string? TrimOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
