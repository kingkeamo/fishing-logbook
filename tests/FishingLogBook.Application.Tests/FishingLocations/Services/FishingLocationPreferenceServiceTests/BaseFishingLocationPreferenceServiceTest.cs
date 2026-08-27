using FishingLogBook.Application.Contracts.Repositories;
using FishingLogBook.Application.FishingLocations.Services;
using FishingLogBook.Domain.FishingLocations;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.FishingLocations.Services.FishingLocationPreferenceServiceTests;

public class BaseFishingLocationPreferenceServiceTest
{
    protected static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid CorribId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    protected static readonly Guid MoyId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");

    protected readonly IFishingLocationPreferenceRepository MockFishingLocationPreferenceRepository =
        Substitute.For<IFishingLocationPreferenceRepository>();

    protected readonly FishingLocationPreferenceService Sut;

    protected BaseFishingLocationPreferenceServiceTest()
    {
        MockFishingLocationPreferenceRepository
            .GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<UserFishingLocationPreference>>([]));
        MockFishingLocationPreferenceRepository
            .ReplaceAsync(
                Arg.Any<Guid>(),
                Arg.Any<IReadOnlyList<UserFishingLocationPreference>>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Ok());
        Sut = new FishingLocationPreferenceService(
            MockFishingLocationPreferenceRepository,
            TestMapper.Create());
    }

    protected static UserFishingLocationPreference Stored(
        Guid id,
        string name,
        bool isDefault = false,
        Guid? userId = null)
    {
        return new UserFishingLocationPreference
        {
            Id = id,
            UserId = userId ?? OwnerUserId,
            Name = name,
            IsDefault = isDefault,
            CreatedOn = DateTimeOffset.Parse("2026-08-27T09:00:00Z")
        };
    }

    protected void GivenStored(params UserFishingLocationPreference[] locations)
    {
        MockFishingLocationPreferenceRepository
            .GetByUserIdAsync(OwnerUserId, Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<UserFishingLocationPreference>>(locations));
    }
}
