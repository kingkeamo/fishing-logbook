using FishingLogBook.Application.Args;
using FishingLogBook.Shared.Dtos;
using FluentResults;

namespace FishingLogBook.Application.Dashboard.Contracts.Services;

public interface IDashboardService
{
    Task<Result<DashboardDto>> GetAsync(GetDashboardArgs args, CancellationToken cancellationToken);
}
