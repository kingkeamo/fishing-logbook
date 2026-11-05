using FishingLogBook.Application.Args;
using FishingLogBook.Domain.Dashboard;
using FluentResults;

namespace FishingLogBook.Application.Dashboard.Contracts.Repositories;

public interface IDashboardRepository
{
    Task<Result<DashboardSnapshot>> GetAsync(GetDashboardArgs args, CancellationToken cancellationToken);
}
