using FishingLogBook.Application.Capabilities.Commands;
using FishingLogBook.Application.Contracts.Services;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Capabilities.Commands.RevokePlatformCapabilityCommandTests;

public class BaseRevokePlatformCapabilityCommandTest
{
    protected readonly IPlatformCapabilityService MockPlatformCapabilityService =
        Substitute.For<IPlatformCapabilityService>();

    protected readonly RevokePlatformCapabilityHandler Sut;

    protected BaseRevokePlatformCapabilityCommandTest()
    {
        Sut = new RevokePlatformCapabilityHandler(MockPlatformCapabilityService, TestMapper.Create());
    }
}
