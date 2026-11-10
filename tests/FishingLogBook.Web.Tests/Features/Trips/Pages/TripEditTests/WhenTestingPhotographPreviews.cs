using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Clients;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Offline.Stores;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TripEditPage = FishingLogBook.Web.Features.Trips.Pages.TripEdit.TripEdit;

namespace FishingLogBook.Web.Tests.Features.Trips.Pages.TripEditTests;

public class WhenTestingPhotographPreviews : BaseTripEditTest
{
    private static TripModel TripWithPhotograph()
    {
        var photograph = new TripPhotographModel(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            TripId,
            OwnerUserId,
            PhotographContentTypeConstants.Jpeg,
            StartedOn.AddHours(1));
        return ActiveTrip() with { Photographs = [photograph] };
    }

    [Fact]
    public async Task ItShouldAskTheServerForPreviewUrlsWhenAStoredPhotographHasNone()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var trip = TripWithPhotograph();
        await using var context = CreateContext(StoreWithTrip(trip));
        var tripClient = context.Services.GetRequiredService<ITripClient>();
        tripClient.GetDetailAsync(TripId, Arg.Any<CancellationToken>())
            .Returns(new TripDetailDto(new TripViewDto(TripId, OwnerUserId, TripConstants.Active, StartedOn))
            {
                Photographs =
                [
                    new TripPhotographViewDto(
                        trip.Photographs[0].Id,
                        PhotographContentTypeConstants.Jpeg,
                        StartedOn.AddHours(1),
                        "https://photos.test/preview")
                ]
            });

        // Act
        var cut = context.Render<TripEditPage>(parameters => parameters.Add(p => p.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#trip-editor").Should().NotBeNull());
        await tripClient.Received(1).GetDetailAsync(TripId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldStillEditTheTripAndLogWhenThePreviewUrlsCannotBeFetched()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var logging = Substitute.For<ILoggingService>();
        await using var context = CreateContext(StoreWithTrip(TripWithPhotograph()), logging: logging);
        var tripClient = context.Services.GetRequiredService<ITripClient>();
        tripClient.GetDetailAsync(TripId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("offline"));

        // Act
        var cut = context.Render<TripEditPage>(parameters => parameters.Add(p => p.TripId, TripId));

        // Assert
        cut.WaitForAssertion(() => cut.Find("#trip-editor").Should().NotBeNull());
        await logging.Received(1).LogErrorAsync(
            "refreshing trip photograph previews",
            Arg.Is<Exception>(exception => exception.Message == "offline"),
            CancellationToken.None);
    }
}
