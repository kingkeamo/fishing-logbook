using FishingLogBook.Application.Capabilities.Contracts.Repositories;
using FishingLogBook.Application.Capabilities.Services;
using FishingLogBook.Application.Common.Contracts.Services;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Capabilities.Services.PlatformCapabilityServiceTests;

public class BasePlatformCapabilityServiceTest
{
    protected static readonly Guid CurrentUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    protected static readonly Guid TargetUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    protected readonly IUserPlatformCapabilityRepository MockUserPlatformCapabilityRepository =
        Substitute.For<IUserPlatformCapabilityRepository>();

    protected readonly ICurrentUser MockCurrentUser = Substitute.For<ICurrentUser>();

    protected readonly PlatformCapabilityService Sut;

    protected BasePlatformCapabilityServiceTest()
    {
        MockCurrentUser.IsResolved.Returns(true);
        MockCurrentUser.UserId.Returns(CurrentUserId);
        Sut = new PlatformCapabilityService(MockUserPlatformCapabilityRepository, MockCurrentUser);
    }
}
