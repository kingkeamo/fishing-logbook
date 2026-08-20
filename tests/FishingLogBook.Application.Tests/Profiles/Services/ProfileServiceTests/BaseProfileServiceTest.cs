using FishingLogBook.Application.Contracts;
using FishingLogBook.Application.Contracts.Repositories;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Application.Profiles.Services;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Profiles.Services.ProfileServiceTests;

public class BaseProfileServiceTest
{
    protected readonly IProfileRepository MockProfileRepository = Substitute.For<IProfileRepository>();
    protected readonly IObjectStorage MockObjectStorage = Substitute.For<IObjectStorage>();
    protected readonly IFishingPreferenceService MockFishingPreferenceService = Substitute.For<IFishingPreferenceService>();
    protected readonly ProfileService Sut;

    protected BaseProfileServiceTest()
    {
        MockObjectStorage.IsConfigured.Returns(true);
        MockFishingPreferenceService
            .GetPreferencesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(new FishingPreferencesDto([])));
        Sut = new ProfileService(MockProfileRepository, MockObjectStorage, MockFishingPreferenceService);
    }
}
