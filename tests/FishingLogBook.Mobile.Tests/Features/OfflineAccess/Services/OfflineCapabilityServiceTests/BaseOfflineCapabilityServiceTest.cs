using FishingLogBook.Mobile.Features.OfflineAccess.Services;
using FishingLogBook.Mobile.Features.OfflineAccess.Stores;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.OfflineAccess.Services.OfflineCapabilityServiceTests;

public abstract class BaseOfflineCapabilityServiceTest
{
    protected static readonly Guid UserId = Guid.Parse("b96ab04e-79c3-44ee-8be8-8dc28578b768");
    protected const string Subject = "cognito-subject";
    protected readonly ISecureOfflineCapabilityStore MockStore =
        Substitute.For<ISecureOfflineCapabilityStore>();
    protected readonly OfflineCapabilityService Sut;

    protected BaseOfflineCapabilityServiceTest()
    {
        Sut = new OfflineCapabilityService(MockStore);
    }
}
