using FishingLogBook.Application.Capabilities.Commands;
using FishingLogBook.Application.Contracts.Services;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Capabilities.Commands.GrantPlatformCapabilityCommandTests;

public class BaseGrantPlatformCapabilityCommandTest
{
    protected readonly IPlatformCapabilityService MockPlatformCapabilityService =
        Substitute.For<IPlatformCapabilityService>();

    protected readonly GrantPlatformCapabilityHandler Sut;

    protected BaseGrantPlatformCapabilityCommandTest()
    {
        Sut = new GrantPlatformCapabilityHandler(MockPlatformCapabilityService, TestMapper.Create());
    }
}
