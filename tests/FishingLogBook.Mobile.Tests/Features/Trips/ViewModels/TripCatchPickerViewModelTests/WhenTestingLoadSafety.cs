using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Trips.Models;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using NSubstitute;
using static FishingLogBook.Mobile.Tests.Features.Trips.TestSupport.TripTestData;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.TripCatchPickerViewModelTests;

public sealed class WhenTestingLoadSafety : BaseTripCatchPickerViewModelTest
{
    [Fact]
    public async Task ItShouldShowLoadingFromTheMomentTheLoadStartsAndClearTheOldList()
    {
        // Arrange
        ServeCandidates(new TripCatchCandidateModel(One, Now.AddHours(-1), "Pike", 2.5m, 60m));
        await Sut.InitializeAsync(Trip(), CancellationToken.None);
        var pending = new TaskCompletionSource<IReadOnlyList<TripCatchCandidateModel>>();
        MockTripCatches.GetEligibleAsync(Arg.Any<LocalTripRecordModel>(), Arg.Any<CancellationToken>()).Returns(pending.Task);
        // Act
        var load = Sut.InitializeAsync(Trip(), CancellationToken.None);
        // Assert
        Sut.IsLoading.Should().BeTrue();
        Sut.Options.Should().BeEmpty();
        pending.SetResult([]);
        await load;
        Sut.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldIgnoreAnOlderLoadThatFinishesAfterANewerOne()
    {
        // Arrange
        var older = new TaskCompletionSource<IReadOnlyList<TripCatchCandidateModel>>();
        MockTripCatches.GetEligibleAsync(Arg.Any<LocalTripRecordModel>(), Arg.Any<CancellationToken>()).Returns(older.Task);
        var olderLoad = Sut.InitializeAsync(Trip(), CancellationToken.None);
        ServeCandidates(new TripCatchCandidateModel(Two, Now.AddHours(-1), "Perch", 1m, 30m));
        // Act
        await Sut.InitializeAsync(Trip(), CancellationToken.None);
        older.SetResult([new TripCatchCandidateModel(One, Now.AddHours(-2), "Pike", 2.5m, 60m)]);
        await olderLoad;
        // Assert
        Sut.Options.Select(option => option.Code).Should().Equal(Two.ToString("D"));
        Sut.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldNotReportAFailureForALoadThatWasReplaced()
    {
        // Arrange
        var older = new TaskCompletionSource<IReadOnlyList<TripCatchCandidateModel>>();
        MockTripCatches.GetEligibleAsync(Arg.Any<LocalTripRecordModel>(), Arg.Any<CancellationToken>()).Returns(older.Task);
        var olderLoad = Sut.InitializeAsync(Trip(), CancellationToken.None);
        ServeCandidates();
        await Sut.InitializeAsync(Trip(), CancellationToken.None);
        // Act
        older.SetException(new InvalidOperationException());
        await olderLoad;
        // Assert
        Sut.HasMessage.Should().BeFalse();
        Sut.IsEmpty.Should().BeTrue();
    }
}
