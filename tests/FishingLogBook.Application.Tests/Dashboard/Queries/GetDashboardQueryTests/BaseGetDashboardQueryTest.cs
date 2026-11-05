using FishingLogBook.Application.Dashboard.Contracts.Services;
using FishingLogBook.Application.Dashboard.Queries;
using FishingLogBook.Application.Tests.Common;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Dashboard.Queries.GetDashboardQueryTests;

public abstract class BaseGetDashboardQueryTest
{
    protected readonly IDashboardService MockService = Substitute.For<IDashboardService>();
    protected readonly GetDashboardHandler Sut;

    protected BaseGetDashboardQueryTest()
    {
        Sut = new GetDashboardHandler(MockService, TestMapper.Create());
    }
}
