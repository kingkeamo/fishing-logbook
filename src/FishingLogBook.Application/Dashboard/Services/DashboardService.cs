using FishingLogBook.Application.Args;
using FishingLogBook.Application.Dashboard.Contracts.Repositories;
using FishingLogBook.Application.Dashboard.Contracts.Services;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using MapsterMapper;

namespace FishingLogBook.Application.Dashboard.Services;

public sealed class DashboardService(IDashboardRepository repository, IMapper mapper) : IDashboardService
{
    public async Task<Result<DashboardDto>> GetAsync(GetDashboardArgs args, CancellationToken cancellationToken)
    {
        var result = await repository.GetAsync(args, cancellationToken);
        return result.IsFailed
            ? Result.Fail<DashboardDto>(result.Errors)
            : Result.Ok(mapper.Map<DashboardDto>(result.Value));
    }
}
