using FishingLogBook.Application.Catches.Contracts.Repositories;
using FishingLogBook.Application.Catches.Contracts.Services;
using FishingLogBook.Application.Catches.Services;
using FishingLogBook.Application.Common.Contracts.Services;
using FishingLogBook.Application.Trips.Contracts.Services;
using FishingLogBook.Domain.Catches;
using FluentResults;
using Microsoft.Extensions.Logging.Abstractions;
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

    protected readonly ICatchPhotographObjectKeyBuilder MockObjectKeyBuilder =
        Substitute.For<ICatchPhotographObjectKeyBuilder>();

    protected readonly CatchService Sut;

    protected static readonly Guid CurrentUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    protected BaseCatchServiceTest()
    {
        MockCurrentUser.IsResolved.Returns(true);
        MockCurrentUser.UserId.Returns(CurrentUserId);
        MockObjectStorage.IsConfigured.Returns(false);
        MockObjectKeyBuilder.Build(Arg.Any<Guid>(), Arg.Any<Guid>())
            .Returns(call => $"catch-photographs/{call.ArgAt<Guid>(0):D}/{call.ArgAt<Guid>(1):D}");
        MockCatchRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Catch?>(null));
        Sut = new CatchService(
            MockCatchRepository,
            MockTripAccessService,
            MockCurrentUser,
            MockCatchLocationPrivacyService,
            MockObjectStorage,
            MockObjectKeyBuilder,
            TestMapper.Create(),
            NullLogger<CatchService>.Instance);
    }
}
