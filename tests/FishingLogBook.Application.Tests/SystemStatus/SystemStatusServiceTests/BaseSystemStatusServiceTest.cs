using FishingLogBook.Application.Contracts;
using FishingLogBook.Application.SystemStatus;
using NSubstitute;

namespace FishingLogBook.Application.Tests.SystemStatus.SystemStatusServiceTests;

public class BaseSystemStatusServiceTest
{
    protected readonly ISystemRepository SystemRepository = Substitute.For<ISystemRepository>();
    protected readonly SystemStatusService Sut;

    protected BaseSystemStatusServiceTest()
    {
        Sut = new SystemStatusService(SystemRepository);
    }
}
