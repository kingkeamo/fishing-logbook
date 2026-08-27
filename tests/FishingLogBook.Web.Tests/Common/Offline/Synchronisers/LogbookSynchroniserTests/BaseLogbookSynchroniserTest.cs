using FishingLogBook.Web.Common.Offline.Synchronisers;
using FishingLogBook.Web.Features.Catch.Offline.Synchronisers;
using FishingLogBook.Web.Features.Catch.Services;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Offline.Synchronisers;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Common.Offline.Synchronisers.LogbookSynchroniserTests;

public class BaseLogbookSynchroniserTest
{
    protected static readonly Guid OwnerUserId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    protected readonly ITripSynchroniser MockTripSynchroniser =
        Substitute.For<ITripSynchroniser>();
    protected readonly ITripPhotographSynchroniser MockTripPhotographSynchroniser =
        Substitute.For<ITripPhotographSynchroniser>();
    protected readonly ICatchSynchroniser MockCatchSynchroniser =
        Substitute.For<ICatchSynchroniser>();
    protected readonly ILocalCatchOwnerService MockLocalCatchOwner =
        Substitute.For<ILocalCatchOwnerService>();
    protected readonly ILoggingService MockLogging = Substitute.For<ILoggingService>();
    protected readonly LogbookSynchroniser Sut;

    protected BaseLogbookSynchroniserTest()
    {
        MockLocalCatchOwner.GetUserIdAsync(Arg.Any<CancellationToken>()).Returns(OwnerUserId);
        Sut = new LogbookSynchroniser(
            MockTripSynchroniser,
            MockTripPhotographSynchroniser,
            MockCatchSynchroniser,
            MockLocalCatchOwner,
            MockLogging);
    }
}
