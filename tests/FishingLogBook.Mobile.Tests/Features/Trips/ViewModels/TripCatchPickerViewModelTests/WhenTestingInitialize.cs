using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Trips.Models;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using NSubstitute;
using static FishingLogBook.Mobile.Tests.Features.Trips.TestSupport.TripTestData;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.TripCatchPickerViewModelTests;

public sealed class WhenTestingInitialize : BaseTripCatchPickerViewModelTest
{
    [Fact]
    public async Task ItShouldExplainWhenTheCandidatesCannotBeRead()
    {
        // Arrange
        MockTripCatches.GetEligibleAsync(Arg.Any<LocalTripRecordModel>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<TripCatchCandidateModel>>(_ => throw new InvalidOperationException());
        // Act
        await Sut.InitializeAsync(Trip(), CancellationToken.None);
        // Assert
        Sut.MessageText.Should().Be(Text["TripAddCatchesLoadFailed"]);
        Sut.IsEmpty.Should().BeFalse();
        Sut.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldShowTheEmptyStateWhenNoCatchCanJoin()
    {
        // Arrange
        ServeCandidates();
        // Act
        await Sut.InitializeAsync(Trip(), CancellationToken.None);
        // Assert
        Sut.IsEmpty.Should().BeTrue();
        Sut.ShowOptions.Should().BeFalse();
        Sut.CanConfirm.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldListCandidatesWithTimeSpeciesAndMeasurements()
    {
        // Arrange
        ServeCandidates(Candidate(One), Candidate(Two, species: null, weight: null));
        // Act
        await Sut.InitializeAsync(Trip(), CancellationToken.None);
        // Assert
        Sut.ShowOptions.Should().BeTrue();
        Sut.Options[0].Name.Should().Be($"{Now.AddHours(-1).DateTime.ToString("g", Text.Culture)} · Pike · 2.5 kg");
        Sut.Options[1].Name.Should().Be($"{Now.AddHours(-1).DateTime.ToString("g", Text.Culture)} · {Text["TripTimelineCatchUnknownSpecies"]}");
        await MockTripCatches.Received(1).GetEligibleAsync(Arg.Is<LocalTripRecordModel>(trip => trip.Detail.Trip.Id == TripId), Arg.Any<CancellationToken>());
    }
}
