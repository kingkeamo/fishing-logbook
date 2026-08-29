using FishingLogBook.Application.Args;
using FishingLogBook.Application.Contracts;
using FishingLogBook.Application.Contracts.Repositories;
using FishingLogBook.Application.Profiles.Services;
using FishingLogBook.Domain.Profiles;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Profiles.Services.AnglerLookupServiceTests;

public class BaseAnglerLookupServiceTest
{
    protected static readonly Guid CurrentUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    protected static readonly Guid MatchedUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    protected readonly IProfileRepository MockProfileRepository = Substitute.For<IProfileRepository>();
    protected readonly IObjectStorage MockObjectStorage = Substitute.For<IObjectStorage>();
    protected readonly AnglerLookupService Sut;

    protected BaseAnglerLookupServiceTest()
    {
        MockObjectStorage.IsConfigured.Returns(true);
        MockObjectStorage.CreateDownloadUrlAsync(
                Arg.Any<string>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
            .Returns(call => new Uri($"https://storage.test/{call.ArgAt<string>(0)}?signed=1"));
        MockProfileRepository.FindAnglersAsync(Arg.Any<FindAnglersArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<AnglerSummary>>([]));
        MockProfileRepository.GetByUserIdsAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<Profile>>([]));
        Sut = new AnglerLookupService(MockProfileRepository, MockObjectStorage);
    }

    protected static AnglerSummary Angler(
        string? displayName = "John Connolly",
        string? photographObjectKey = null,
        string? homeRegion = null)
    {
        return new AnglerSummary
        {
            UserId = MatchedUserId,
            DisplayName = displayName,
            PhotographObjectKey = photographObjectKey,
            HomeRegion = homeRegion
        };
    }
}
