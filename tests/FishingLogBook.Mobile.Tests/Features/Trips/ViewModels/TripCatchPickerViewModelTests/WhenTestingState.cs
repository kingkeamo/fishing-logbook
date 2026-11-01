using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Features.Trips.Models;
using FishingLogBook.Mobile.Features.Trips.Offline.Models;
using FishingLogBook.Shared.Enums;
using NSubstitute;
using static FishingLogBook.Mobile.Tests.Features.Trips.TestSupport.TripTestData;

namespace FishingLogBook.Mobile.Tests.Features.Trips.ViewModels.TripCatchPickerViewModelTests;

public sealed class WhenTestingState : BaseTripCatchPickerViewModelTest
{
    [Fact]
    public void ItShouldStartLoadingWithNothingToConfirm()
    {
        // Arrange
        // Act
        // Assert
        Sut.IsLoading.Should().BeTrue();
        Sut.IsEmpty.Should().BeFalse();
        Sut.ShowOptions.Should().BeFalse();
        Sut.HasMessage.Should().BeFalse();
        Sut.MessageText.Should().BeEmpty();
        Sut.CanConfirm.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldShowThePoundAndInchLabelsWhenThoseAreTheAccountUnits()
    {
        // Arrange
        MockUnits.GetAsync(Arg.Any<CancellationToken>()).Returns(new MeasurementUnitsModel(WeightUnitEnum.Lb, LengthUnitEnum.In));
        ServeCandidates(new TripCatchCandidateModel(One, Now.AddHours(-1), "Pike", 2m, 50m));
        // Act
        await Sut.InitializeAsync(Trip(), CancellationToken.None);
        // Assert
        Sut.Options.Single().Name.Should().EndWith($"· 4.41 {Text["MeasureUnitLb"]} · 19.69 {Text["MeasureUnitIn"]}");
    }

    [Fact]
    public async Task ItShouldShowOnlyTheLengthWhenACatchHasNoWeight()
    {
        // Arrange
        ServeCandidates(new TripCatchCandidateModel(One, Now.AddHours(-1), "Pike", null, 80m));
        // Act
        await Sut.InitializeAsync(Trip(), CancellationToken.None);
        // Assert
        Sut.Options.Single().Name.Should().EndWith($"· Pike · 80 {Text["MeasureUnitCm"]}");
    }

    [Fact]
    public async Task ItShouldIgnoreATapOnACatchThatIsNotListed()
    {
        // Arrange
        ServeCandidates(Candidate(One));
        await Sut.InitializeAsync(Trip(), CancellationToken.None);
        // Act
        Sut.Toggle(Two.ToString("D"));
        // Assert
        Sut.SelectedCount.Should().Be(0);
    }

    [Fact]
    public async Task ItShouldIgnoreTapsWhileTheSelectionIsBeingAdded()
    {
        // Arrange
        ServeCandidates(Candidate(One), Candidate(Two));
        await Sut.InitializeAsync(Trip(), CancellationToken.None);
        Sut.Toggle(One.ToString("D"));
        var release = new TaskCompletionSource<TripCatchChangeModel>();
        MockTripCatches.AssociateAsync(Arg.Any<LocalTripRecordModel>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>()).Returns(release.Task);
        var confirming = Sut.ConfirmAsync(CancellationToken.None);
        // Act
        Sut.Toggle(Two.ToString("D"));
        var duringSave = (Sut.SelectedCount, Sut.CanConfirm);
        release.SetResult(new TripCatchChangeModel([One], []));
        await confirming;
        // Assert
        duringSave.Should().Be((1, false));
    }

    [Fact]
    public async Task ItShouldNotBeEmptyWhileAMessageExplainsWhy()
    {
        // Arrange
        ServeCandidates();
        await Sut.InitializeAsync(Trip(), CancellationToken.None);
        // Act
        var empty = Sut.IsEmpty;
        MockTripCatches.GetEligibleAsync(Arg.Any<LocalTripRecordModel>(), Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<TripCatchCandidateModel>>(_ => throw new InvalidOperationException());
        await Sut.InitializeAsync(Trip(), CancellationToken.None);
        // Assert
        empty.Should().BeTrue();
        Sut.HasMessage.Should().BeTrue();
        Sut.IsEmpty.Should().BeFalse();
    }
}
