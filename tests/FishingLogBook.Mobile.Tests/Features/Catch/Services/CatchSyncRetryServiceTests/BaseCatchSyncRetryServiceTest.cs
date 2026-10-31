using FishingLogBook.Mobile.Common.Offline.Coordinators;
using FishingLogBook.Mobile.Common.Offline.Services;
using FishingLogBook.Mobile.Common.Offline.Stores;
using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Catch.Services;
using FishingLogBook.Mobile.Tests.Support;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Catch.Services.CatchSyncRetryServiceTests;

public abstract class BaseCatchSyncRetryServiceTest
{
    protected static readonly Guid UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    protected static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-17T10:00:00Z");

    protected readonly IAccountSessionCoordinator MockSession = Substitute.For<IAccountSessionCoordinator>();
    protected readonly IOutboxStore MockOutboxStore = Substitute.For<IOutboxStore>();
    protected readonly IOutboxSyncCoordinator MockSyncCoordinator = Substitute.For<IOutboxSyncCoordinator>();
    protected readonly IOutboxChangeNotifier MockNotifier = Substitute.For<IOutboxChangeNotifier>();
    protected readonly CatchSyncRetryService Sut;

    protected BaseCatchSyncRetryServiceTest()
    {
        Sut = new CatchSyncRetryService(
            MockSession,
            MockOutboxStore,
            MockSyncCoordinator,
            MockNotifier,
            new FixedTimeProvider(Now));
    }

    protected void SignIn()
    {
        MockSession.Capture().Returns(new AccountSessionLeaseModel(UserId, 1, CancellationToken.None));
    }
}
