using FishingLogBook.Application.Contracts;
using FishingLogBook.Application.Contracts.Repositories;
using FishingLogBook.Application.Profiles.Services;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Profiles.Services.ProfileServiceTests;

public class BaseProfileServiceTest
{
    protected readonly IProfileRepository MockProfileRepository = Substitute.For<IProfileRepository>();
    protected readonly IObjectStorage MockObjectStorage = Substitute.For<IObjectStorage>();
    protected readonly ProfileService Sut;

    protected BaseProfileServiceTest()
    {
        MockObjectStorage.IsConfigured.Returns(true);
        Sut = new ProfileService(MockProfileRepository, MockObjectStorage);
    }
}
