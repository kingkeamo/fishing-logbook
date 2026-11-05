using FishingLogBook.Mobile.Features.Authentication.Coordinators;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Photographs.Models;
using FishingLogBook.Mobile.Features.Photographs.Services;
using FishingLogBook.Mobile.Features.Trips.Services;
using FishingLogBook.Mobile.Tests.Support;
using FishingLogBook.Shared.Photographs.Models;
using NSubstitute;
using static FishingLogBook.Mobile.Tests.Features.Trips.TestSupport.TripTestData;

namespace FishingLogBook.Mobile.Tests.Features.Trips.Services.TripPhotographServiceTests;

public abstract class BaseTripPhotographServiceTest
{
    protected readonly ITripService MockTrips = Substitute.For<ITripService>();
    protected readonly IPhotographCollectionEditorService MockPhotographs = Substitute.For<IPhotographCollectionEditorService>();
    protected readonly IAccountSessionCoordinator MockSession = Substitute.For<IAccountSessionCoordinator>();
    protected readonly CapturingLogger<TripPhotographService> Logger = new();
    protected readonly TripPhotographService Sut;

    protected BaseTripPhotographServiceTest()
    {
        MockSession.Capture().Returns(new AccountSessionLeaseModel(ViewerId, 1, CancellationToken.None));
        Sut = new TripPhotographService(MockTrips, MockPhotographs, MockSession, new FixedTimeProvider(Now), Logger);
    }

    protected static PhotographEditorItemModel Prepared(Guid id, DateTimeOffset? capturedOn = null)
    {
        return new PhotographEditorItemModel
        {
            Id = id,
            SortOrder = 0,
            PreviewPath = "/abs/p.jpg",
            RelativeFilePath = "photos/p.jpg",
            ContentType = "image/jpeg",
            Metadata = new PhotographMetadataModel(capturedOn, null, null)
        };
    }
}
