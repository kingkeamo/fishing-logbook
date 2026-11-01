using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Trips.Models;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using NSubstitute;
using static FishingLogBook.Mobile.Tests.Features.Trips.TestSupport.TripTestData;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.TripCatchPickerViewModelTests;

public sealed class WhenTestingConfirm : BaseTripCatchPickerViewModelTest
{
    private async Task OpenAsync(params TripCatchCandidateModel[] candidates)
    {
        ServeCandidates(candidates);
        await Sut.InitializeAsync(Trip(), CancellationToken.None);
    }

    [Fact]
    public async Task ItShouldNotConfirmWithoutASelection()
    {
        // Arrange
        await OpenAsync(Candidate(One));
        // Act
        var done = await Sut.ConfirmAsync(CancellationToken.None);
        // Assert
        done.Should().BeFalse();
        await MockTripCatches.DidNotReceive().AssociateAsync(Arg.Any<LocalTripRecordModel>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldTrackTheSelectionAndLabelTheConfirmButton()
    {
        // Arrange
        await OpenAsync(Candidate(One), Candidate(Two));
        // Act
        Sut.Toggle(One.ToString("D"));
        var one = (Sut.SelectedCount, Sut.ConfirmText);
        Sut.Toggle(Two.ToString("D"));
        var two = (Sut.SelectedCount, Sut.ConfirmText);
        Sut.Toggle(One.ToString("D"));
        // Assert
        one.Should().Be((1, Text["TripAddCatchesConfirmOne"]));
        two.Should().Be((2, string.Format(Text.Culture, Text["TripAddCatchesConfirmMany"], 2)));
        Sut.Options.Select(option => option.IsSelected).Should().Equal(false, true);
        Sut.CanConfirm.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldRefreshTheListWithAWarningWhenEveryCatchWasRejected()
    {
        // Arrange
        await OpenAsync(Candidate(One));
        MockTripCatches.AssociateAsync(Arg.Any<LocalTripRecordModel>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new TripCatchChangeModel([], [One]));
        Sut.Toggle(One.ToString("D"));
        // Act
        var done = await Sut.ConfirmAsync(CancellationToken.None);
        // Assert
        done.Should().BeFalse();
        Sut.MessageText.Should().Be(Text["TripAddCatchesRejected"]);
        await MockTripCatches.Received(2).GetEligibleAsync(Arg.Any<LocalTripRecordModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldReportAFailureAndStayOpenWhenAddingTheCatchesThrows()
    {
        // Arrange
        await OpenAsync(Candidate(One));
        MockTripCatches.AssociateAsync(Arg.Any<LocalTripRecordModel>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns<TripCatchChangeModel>(_ => throw new InvalidOperationException());
        Sut.Toggle(One.ToString("D"));
        // Act
        var done = await Sut.ConfirmAsync(CancellationToken.None);
        // Assert
        done.Should().BeFalse();
        Sut.MessageText.Should().Be(Text["TripAddCatchesFailed"]);
        Sut.CanConfirm.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldAddTheSelectedCatchesAndSayTheModalCanClose()
    {
        // Arrange
        await OpenAsync(Candidate(One), Candidate(Two));
        MockTripCatches.AssociateAsync(Arg.Any<LocalTripRecordModel>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new TripCatchChangeModel([Two], []));
        Sut.Toggle(Two.ToString("D"));
        // Act
        var done = await Sut.ConfirmAsync(CancellationToken.None);
        // Assert
        done.Should().BeTrue();
        await MockTripCatches.Received(1).AssociateAsync(
            Arg.Is<LocalTripRecordModel>(trip => trip.Detail.Trip.Id == TripId),
            Arg.Is<IReadOnlyList<Guid>>(ids => ids.SequenceEqual(new[] { Two })),
            Arg.Any<CancellationToken>());
    }
}
