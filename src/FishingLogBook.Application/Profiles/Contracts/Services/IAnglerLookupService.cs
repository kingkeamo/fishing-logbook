using FishingLogBook.Application.Args;
using FishingLogBook.Shared.Dtos;
using FluentResults;

namespace FishingLogBook.Application.Profiles.Contracts.Services;

public interface IAnglerLookupService
{
    Task<Result<IReadOnlyList<AnglerSummaryDto>>> FindAsync(
        FindAnglersArgs args,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyDictionary<Guid, AnglerSummaryDto>>> DescribeAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken);
}
