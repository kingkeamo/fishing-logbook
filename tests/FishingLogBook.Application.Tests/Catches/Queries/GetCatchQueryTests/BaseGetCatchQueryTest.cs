using FishingLogBook.Application.Catches.Queries;
using FishingLogBook.Application.Contracts.Services;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Catches.Queries.GetCatchQueryTests;

public class BaseGetCatchQueryTest
{
    protected readonly ICatchService MockCatchService = Substitute.For<ICatchService>();

    protected readonly GetCatchHandler Sut;

    protected BaseGetCatchQueryTest()
    {
        Sut = new GetCatchHandler(MockCatchService, TestMapper.Create());
    }
}
