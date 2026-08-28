using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Web.Features.Trips.Enums;
using FishingLogBook.Web.Features.Trips.Modals.AddTripCatches;
using FishingLogBook.Web.Features.Trips.Models;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Tests.TestSupport;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Trips.Modals.AddTripCatchesModalTests;

public class WhenTestingSelect : BaseAddTripCatchesModalTest
{
    [Fact]
    public async Task ItShouldShowACleanEmptyStateWhenNothingCanBeAdded()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var tripCatches = TripCatchesOffering();
        await using var context = CreateContext(tripCatches);

        // Act
        var (cut, dialog) = await ShowModalAsync(context);

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#trip-catches-empty").TextContent.Should().Contain("No catches to add"));
        cut.Find("#trip-catches-empty").TextContent.Should()
            .Contain("All catches recorded during this trip are already associated.");
        cut.FindAll("#catch-selector").Should().BeEmpty();
        cut.FindAll("#trip-catches-confirm").Should().BeEmpty();
        dialog.Result.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldOnlyOfferTheCatchesTheTripServiceFoundEligible()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var tripCatches = TripCatchesOffering(
            Catch(PikeCatchId, "Pike"),
            Catch(TroutCatchId, "Brown Trout"));
        await using var context = CreateContext(tripCatches);

        // Act
        var (cut, _) = await ShowModalAsync(context);

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#trip-catches-subtitle").TextContent
                .Should().Contain("Select catches to add to this trip"));
        cut.Find($"#catch-selector-option-{PikeCatchId:D}").Should().NotBeNull();
        cut.Find($"#catch-selector-option-{TroutCatchId:D}").Should().NotBeNull();
        await tripCatches.Received(1).GetEligibleAsync(
            Arg.Is<TripCatchScopeModel>(scope =>
                scope.TripId == TripId
                && scope.OwnerUserId == OwnerUserId
                && scope.StartedOn == StartedOn
                && scope.EndedOn == EndedOn),
            TripStorageEnum.LocalFirst,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldKeepTheConfirmActionDisabledUntilACatchIsChosen()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext(TripCatchesOffering(Catch(PikeCatchId, "Pike")));

        // Act
        var (cut, _) = await ShowModalAsync(context);

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#trip-catches-confirm").HasAttribute("disabled").Should().BeTrue());
    }

    [Fact]
    public async Task ItShouldCountTheChosenCatchesOnTheConfirmAction()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        await using var context = CreateContext(TripCatchesOffering(
            Catch(PikeCatchId, "Pike"),
            Catch(TroutCatchId, "Brown Trout")));
        var (cut, _) = await ShowModalAsync(context);
        cut.WaitForAssertion(() => cut.Find($"#catch-selector-option-{PikeCatchId:D}").Should().NotBeNull());

        // Act
        cut.Find($"#catch-selector-option-{PikeCatchId:D}").Change(true);

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#trip-catches-confirm").TextContent.Should().Contain("Add 1 catch"));
        cut.Find($"#catch-selector-option-{TroutCatchId:D}").Change(true);
        cut.WaitForAssertion(() =>
            cut.Find("#trip-catches-confirm").TextContent.Should().Contain("Add 2 catches"));
    }

    [Fact]
    public async Task ItShouldShowTheFailureWhenTheEligibleCatchesCannotBeRead()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var tripCatches = Substitute.For<Web.Features.Trips.Services.ITripCatchService>();
        tripCatches.GetEligibleAsync(
                Arg.Any<TripCatchScopeModel>(),
                Arg.Any<TripStorageEnum>(),
                Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<Web.Features.Catch.Models.CatchModel>>(
                _ => throw new InvalidOperationException("read failed"));
        var logging = Substitute.For<Web.Features.Diagnostics.Services.ILoggingService>();
        await using var context = CreateContext(tripCatches, logging);

        // Act
        var (cut, _) = await ShowModalAsync(context);

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#trip-catches-load-failed").TextContent.Should().Contain("could not be read"));
        await logging.Received(1).LogErrorAsync(
            "reading the catches that can join a trip",
            Arg.Any<Exception>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldShowFrenchCatchPickerCopy()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.French);
        await using var context = CreateContext(TripCatchesOffering());

        // Act
        var (cut, _) = await ShowModalAsync(context);

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#trip-catches-empty").TextContent.Should().Contain("Aucune prise à ajouter"));
        cut.Find("#trip-catches-modal-title").TextContent.Should().Contain("Ajouter des prises");
    }
}
