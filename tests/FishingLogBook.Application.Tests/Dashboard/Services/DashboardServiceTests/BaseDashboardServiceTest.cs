using FishingLogBook.Application.Dashboard.Contracts.Repositories;
using FishingLogBook.Application.Dashboard.Services;
using FishingLogBook.Application.Tests.Common;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Dashboard.Services.DashboardServiceTests;

public abstract class BaseDashboardServiceTest
{
    protected readonly IDashboardRepository MockRepository = Substitute.For<IDashboardRepository>();
    protected readonly DashboardService Sut;

    protected BaseDashboardServiceTest()
    {
        Sut = new DashboardService(MockRepository, TestMapper.Create());
    }
}
