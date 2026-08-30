using FishingLogBook.Application.Catches.Contracts.Services;
using FishingLogBook.Application.Catches.Queries;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Catches.Queries.GetMyCatchesQueryTests;

public class BaseGetMyCatchesQueryTest
{
    protected readonly ICatchService MockCatchService = Substitute.For<ICatchService>();

    protected readonly GetMyCatchesHandler Sut;

    protected BaseGetMyCatchesQueryTest()
    {
        Sut = new GetMyCatchesHandler(MockCatchService, TestMapper.Create());
    }
}
