using FishingLogBook.Application.Args;
using FishingLogBook.Application.Common.Contracts.Services;
using FishingLogBook.Application.Profiles.Contracts.Repositories;
using FishingLogBook.Application.Profiles.Contracts.Services;
using FishingLogBook.Application.Profiles.Errors;
using FishingLogBook.Domain.Profiles;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FluentResults;

namespace FishingLogBook.Application.Profiles.Services;

public sealed class AnglerLookupService : IAnglerLookupService
{
    private static readonly TimeSpan DownloadLifetime = TimeSpan.FromMinutes(15);

    private readonly IProfileRepository _profileRepository;
    private readonly IObjectStorage _objectStorage;

    public AnglerLookupService(IProfileRepository profileRepository, IObjectStorage objectStorage)
    {
        _profileRepository = profileRepository;
        _objectStorage = objectStorage;
    }

    public async Task<Result<IReadOnlyList<AnglerSummaryDto>>> FindAsync(
        FindAnglersArgs args,
        CancellationToken cancellationToken)
    {
        var query = AnglerLookupConstants.TrimQuery(args.Query);
        if (query is null || !AnglerLookupConstants.IsQueryValid(query))
        {
            return Result.Fail<IReadOnlyList<AnglerSummaryDto>>(new AnglerLookupQueryInvalidError());
        }

        var matched = await _profileRepository.FindAnglersAsync(
            new FindAnglersArgs
            {
                RequestingUserId = args.RequestingUserId,
                Query = query,
                MaxResults = Bounded(args.MaxResults)
            },
            cancellationToken);
        if (matched.IsFailed)
        {
            return Result.Fail<IReadOnlyList<AnglerSummaryDto>>(matched.Errors);
        }

        var summaries = new List<AnglerSummaryDto>(matched.Value.Count);
        foreach (var angler in matched.Value)
        {
            summaries.Add(await ToDtoAsync(angler, cancellationToken));
        }

        return Result.Ok<IReadOnlyList<AnglerSummaryDto>>(summaries);
    }

    public async Task<Result<IReadOnlyDictionary<Guid, AnglerSummaryDto>>> DescribeAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken)
    {
        var wanted = userIds.Where(userId => userId != Guid.Empty).Distinct().ToArray();
        if (wanted.Length == 0)
        {
            return Result.Ok<IReadOnlyDictionary<Guid, AnglerSummaryDto>>(
                new Dictionary<Guid, AnglerSummaryDto>());
        }

        var profiles = await _profileRepository.GetByUserIdsAsync(wanted, cancellationToken);
        if (profiles.IsFailed)
        {
            return Result.Fail<IReadOnlyDictionary<Guid, AnglerSummaryDto>>(profiles.Errors);
        }

        var described = new Dictionary<Guid, AnglerSummaryDto>(profiles.Value.Count);
        foreach (var profile in profiles.Value)
        {
            described[profile.UserId] = await ToDtoAsync(
                new AnglerSummary
                {
                    UserId = profile.UserId,
                    DisplayName = profile.ShowDisplayName ? profile.DisplayName : null,
                    PhotographObjectKey = profile.ShowPhotograph ? profile.PhotographObjectKey : null,
                    HomeRegion = profile.ShowHomeRegion ? profile.HomeRegion : null
                },
                cancellationToken);
        }

        return Result.Ok<IReadOnlyDictionary<Guid, AnglerSummaryDto>>(described);
    }

    private static int Bounded(int maxResults)
    {
        if (maxResults <= 0)
        {
            return AnglerLookupConstants.MaxResults;
        }

        return Math.Min(maxResults, AnglerLookupConstants.MaxResults);
    }

    private async Task<AnglerSummaryDto> ToDtoAsync(AnglerSummary angler, CancellationToken cancellationToken)
    {
        return new AnglerSummaryDto(
            angler.UserId,
            angler.DisplayName,
            await CreateDownloadUrlAsync(angler.PhotographObjectKey, cancellationToken),
            angler.HomeRegion,
            angler.Email);
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
