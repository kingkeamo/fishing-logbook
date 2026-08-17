using FishingLogBook.Application.Catches.Services;
using FishingLogBook.Application.Contracts.Repositories;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Catches.Services.CatchServiceTests;

public class BaseCatchServiceTest
{
    protected readonly ICatchRepository MockCatchRepository = Substitute.For<ICatchRepository>();

    protected readonly CatchService Sut;

    protected BaseCatchServiceTest()
    {
        Sut = new CatchService(MockCatchRepository);
    }
}
