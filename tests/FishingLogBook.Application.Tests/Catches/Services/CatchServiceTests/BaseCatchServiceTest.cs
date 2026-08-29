using FishingLogBook.Application.Catches.Services;
using FishingLogBook.Application.Contracts;
using FishingLogBook.Application.Contracts.Repositories;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Domain.Catches;
using FluentResults;
using NSubstitute;

namespace FishingLogBook.Application.Tests.Catches.Services.CatchServiceTests;

public class BaseCatchServiceTest
{
    protected readonly ICatchRepository MockCatchRepository = Substitute.For<ICatchRepository>();

    protected readonly ITripAccessService MockTripAccessService = Substitute.For<ITripAccessService>();

    protected readonly ICurrentUser MockCurrentUser = Substitute.For<ICurrentUser>();

    protected readonly ICatchLocationPrivacyService MockCatchLocationPrivacyService =
        Substitute.For<ICatchLocationPrivacyService>();

    protected readonly IObjectStorage MockObjectStorage = Substitute.For<IObjectStorage>();

    protected readonly CatchService Sut;

    protected static readonly Guid CurrentUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    protected BaseCatchServiceTest()
    {
        MockCurrentUser.IsResolved.Returns(true);
        MockCurrentUser.UserId.Returns(CurrentUserId);
        MockObjectStorage.IsConfigured.Returns(false);
        MockCatchRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Catch?>(null));
        Sut = new CatchService(
            MockCatchRepository,
            MockTripAccessService,
            MockCurrentUser,
            MockCatchLocationPrivacyService,
            MockObjectStorage,
            TestMapper.Create());
    }
}
