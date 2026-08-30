using FishingLogBook.Application.Common.Contracts.Services;
using FishingLogBook.Application.FishingPreferences.Contracts.Services;
using FishingLogBook.Application.Profiles.Contracts.Repositories;
using FishingLogBook.Application.Profiles.Contracts.Services;
using FishingLogBook.Application.Profiles.Services;
using FishingLogBook.Shared.Dtos;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Profiles.Services.ProfileServiceTests;

public class BaseProfileServiceTest
{
    protected readonly IProfileRepository MockProfileRepository = Substitute.For<IProfileRepository>();
    protected readonly IObjectStorage MockObjectStorage = Substitute.For<IObjectStorage>();
    protected readonly IProfilePhotographObjectKeyBuilder MockObjectKeyBuilder =
        Substitute.For<IProfilePhotographObjectKeyBuilder>();
    protected readonly IFishingPreferenceService MockFishingPreferenceService = Substitute.For<IFishingPreferenceService>();
    protected readonly ProfileService Sut;

    protected BaseProfileServiceTest()
    {
        MockObjectStorage.IsConfigured.Returns(true);
        MockObjectKeyBuilder.Build(Arg.Any<Guid>(), Arg.Any<Guid>())
            .Returns(call => $"profiles/{call.ArgAt<Guid>(0):D}/{call.ArgAt<Guid>(1):D}");
        MockFishingPreferenceService
            .GetPreferencesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(new FishingPreferencesDto([])));
        Sut = new ProfileService(
            MockProfileRepository,
            MockObjectStorage,
            MockObjectKeyBuilder,
            MockFishingPreferenceService);
    }
}
