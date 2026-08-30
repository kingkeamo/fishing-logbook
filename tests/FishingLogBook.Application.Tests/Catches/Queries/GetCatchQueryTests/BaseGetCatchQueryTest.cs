using FishingLogBook.Application.Catches.Contracts.Services;
using FishingLogBook.Application.Catches.Queries;
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
