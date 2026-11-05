using FishingLogBook.Application.Dashboard.Contracts.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace FishingLogBook.Api.Tests.DashboardEndpointsTests;

public sealed class DashboardApiFactory : SystemApiFactory
{
    public IDashboardRepository DashboardRepository { get; } = Substitute.For<IDashboardRepository>();

    protected override void ConfigureAdditionalTestServices(IServiceCollection services)
    {
        services.RemoveAll<IDashboardRepository>();
        services.AddSingleton(DashboardRepository);
    }
}
