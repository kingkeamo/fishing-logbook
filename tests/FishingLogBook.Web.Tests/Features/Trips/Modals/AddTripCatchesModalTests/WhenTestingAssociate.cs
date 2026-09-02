using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Trips.Enums;
using FishingLogBook.Web.Features.Trips.Modals.AddTripCatches;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Features.Trips.Services;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.TestSupport;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Modals.AddTripCatchesModalTests;

public class WhenTestingAssociate : BaseAddTripCatchesModalTest
{
    [Fact]
    public async Task ItShouldAddNothingWhenTheAnglerCancels()
    {
        // Arrange
        var tripCatches = TripCatchesOffering(Catch(PikeCatchId, "Pike"));
        await using var context = CreateContext(tripCatches);
        var (cut, dialog) = await ShowModalAsync(context);
        cut.WaitForAssertion(() => cut.Find($"#catch-selector-option-{PikeCatchId:D}").Should().NotBeNull());
        cut.Find($"#catch-selector-option-{PikeCatchId:D}").Change(true);

        // Act
        await cut.Find("#trip-catches-cancel").ClickAsync();

        // Assert
        var result = await dialog.Result;
        result.Should().NotBeNull();
        result!.Canceled.Should().BeTrue();
        await tripCatches.DidNotReceive().AssociateAsync(
            Arg.Any<TripCatchScopeModel>(),
            Arg.Any<IReadOnlyList<Guid>>(),
            Arg.Any<TripStorageEnum>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAssociateEverySelectedCatchInOneAction()
    {
        // Arrange
        var tripCatches = TripCatchesOffering(
            Catch(PikeCatchId, "Pike"),
            Catch(TroutCatchId, "Brown Trout"));
        await using var context = CreateContext(tripCatches);
        var (cut, dialog) = await ShowModalAsync(context);
        cut.WaitForAssertion(() => cut.Find($"#catch-selector-option-{PikeCatchId:D}").Should().NotBeNull());
        cut.Find($"#catch-selector-option-{PikeCatchId:D}").Change(true);
        cut.Find($"#catch-selector-option-{TroutCatchId:D}").Change(true);

        // Act
        await cut.Find("#trip-catches-confirm").ClickAsync();

        // Assert
        await tripCatches.Received(1).AssociateAsync(
            Arg.Is<TripCatchScopeModel>(scope => scope.TripId == TripId),
            Arg.Is<IReadOnlyList<Guid>>(ids =>
                ids.Count == 2 && ids.Contains(PikeCatchId) && ids.Contains(TroutCatchId)),
            TripStorageEnum.LocalFirst,
            Arg.Any<CancellationToken>());
        var result = await dialog.Result;
        result!.Canceled.Should().BeFalse();
        result.Data.Should().BeOfType<AddTripCatchesModalResult>()
            .Which.AssociatedCatchIds.Should().Equal(PikeCatchId, TroutCatchId);
    }

    [Fact]
    public async Task ItShouldUseTheServerForAHistoricalTrip()
    {
        // Arrange
        var tripCatches = TripCatchesOffering(Catch(PikeCatchId, "Pike"));
        await using var context = CreateContext(tripCatches);
        var (cut, _) = await ShowModalAsync(context, TripStorageEnum.Server);
        cut.WaitForAssertion(() => cut.Find($"#catch-selector-option-{PikeCatchId:D}").Should().NotBeNull());
        cut.Find($"#catch-selector-option-{PikeCatchId:D}").Change(true);

        // Act
        await cut.Find("#trip-catches-confirm").ClickAsync();

        // Assert
        await tripCatches.Received(1).GetEligibleAsync(
            Arg.Any<TripCatchScopeModel>(),
            TripStorageEnum.Server,
            Arg.Any<CancellationToken>());
        await tripCatches.Received(1).AssociateAsync(
            Arg.Any<TripCatchScopeModel>(),
            Arg.Is<IReadOnlyList<Guid>>(ids => ids.Count == 1 && ids[0] == PikeCatchId),
            TripStorageEnum.Server,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRefreshTheListWhenEverySelectedCatchWasRefused()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var tripCatches = TripCatchesOffering(Catch(PikeCatchId, "Pike"));
        tripCatches.AssociateAsync(
                Arg.Any<TripCatchScopeModel>(),
                Arg.Any<IReadOnlyList<Guid>>(),
                Arg.Any<TripStorageEnum>(),
                Arg.Any<CancellationToken>())
            .Returns(new TripCatchAssociationModel([], [PikeCatchId]));
        await using var context = CreateContext(tripCatches);
        var (cut, dialog) = await ShowModalAsync(context, TripStorageEnum.Server);
        cut.WaitForAssertion(() => cut.Find($"#catch-selector-option-{PikeCatchId:D}").Should().NotBeNull());
        cut.Find($"#catch-selector-option-{PikeCatchId:D}").Change(true);

        // Act
        await cut.Find("#trip-catches-confirm").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#trip-catches-rejected").TextContent.Should().Contain("no longer available"));
        dialog.Result.IsCompleted.Should().BeFalse();
        await tripCatches.Received(2).GetEligibleAsync(
            Arg.Any<TripCatchScopeModel>(),
            Arg.Any<TripStorageEnum>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldAskTheAnglerToGoOnlineWhenTheServerCannotBeReached()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var tripCatches = TripCatchesOffering(Catch(PikeCatchId, "Pike"));
        tripCatches.AssociateAsync(
                Arg.Any<TripCatchScopeModel>(),
                Arg.Any<IReadOnlyList<Guid>>(),
                Arg.Any<TripStorageEnum>(),
                Arg.Any<CancellationToken>())
            .Returns<TripCatchAssociationModel>(_ => throw new HttpRequestException("offline"));
        var logging = Substitute.For<ILoggingService>();
        await using var context = CreateContext(tripCatches, logging);
        var (cut, dialog) = await ShowModalAsync(context, TripStorageEnum.Server);
        cut.WaitForAssertion(() => cut.Find($"#catch-selector-option-{PikeCatchId:D}").Should().NotBeNull());
        cut.Find($"#catch-selector-option-{PikeCatchId:D}").Change(true);

        // Act
        await cut.Find("#trip-catches-confirm").ClickAsync();

        // Assert
        cut.Find("#trip-catches-failed").TextContent
            .Should().Contain("You need to be online to add catches to this trip.");
        dialog.Result.IsCompleted.Should().BeFalse();
        await logging.Received(1).LogErrorAsync(
            "adding catches to a trip",
            Arg.Any<Exception>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowTheFailureWhenALocalAssociationFails()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var tripCatches = TripCatchesOffering(Catch(PikeCatchId, "Pike"));
        tripCatches.AssociateAsync(
                Arg.Any<TripCatchScopeModel>(),
                Arg.Any<IReadOnlyList<Guid>>(),
                Arg.Any<TripStorageEnum>(),
                Arg.Any<CancellationToken>())
            .Returns<TripCatchAssociationModel>(_ => throw new InvalidOperationException("write failed"));
        await using var context = CreateContext(tripCatches);
        var (cut, dialog) = await ShowModalAsync(context);
        cut.WaitForAssertion(() => cut.Find($"#catch-selector-option-{PikeCatchId:D}").Should().NotBeNull());
        cut.Find($"#catch-selector-option-{PikeCatchId:D}").Change(true);

        // Act
        await cut.Find("#trip-catches-confirm").ClickAsync();

        // Assert
        cut.Find("#trip-catches-failed").TextContent
            .Should().Contain("could not be added to the trip");
        dialog.Result.IsCompleted.Should().BeFalse();
    }

    private static CatchModel Catch(Guid catchId, string speciesName)
    {
        return new CatchModel(
            catchId,
            StartedOn.AddHours(2),
            [new CatchPhotographModel(Guid.NewGuid(), catchId, PhotographContentTypeConstants.Jpeg)],
            speciesName,
            CaughtByUserId: OwnerUserId);
    }
}
