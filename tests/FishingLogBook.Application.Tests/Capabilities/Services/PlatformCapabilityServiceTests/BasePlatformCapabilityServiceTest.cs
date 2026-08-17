using FishingLogBook.Application.Capabilities.Services;
using FishingLogBook.Application.Contracts.Repositories;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Capabilities.Services.PlatformCapabilityServiceTests;

public class BasePlatformCapabilityServiceTest
{
    protected readonly IUserPlatformCapabilityRepository MockUserPlatformCapabilityRepository =
        Substitute.For<IUserPlatformCapabilityRepository>();

    protected readonly PlatformCapabilityService Sut;

    protected BasePlatformCapabilityServiceTest()
    {
        Sut = new PlatformCapabilityService(MockUserPlatformCapabilityRepository);
    }
}
