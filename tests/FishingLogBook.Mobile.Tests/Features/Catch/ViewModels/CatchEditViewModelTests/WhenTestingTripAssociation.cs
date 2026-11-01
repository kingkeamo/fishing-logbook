using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Catch.Offline.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Models;
using FishingLogBook.Mobile.Features.Photographs.Models;
using FishingLogBook.Mobile.Features.Trips.Models;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Catch.ViewModels.CatchEditViewModelTests;

public sealed class WhenTestingTripAssociation : BaseCatchEditViewModelTest
{
    private static readonly Guid TripId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtherAnglerId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static TripAssociationModel Trip(params TripAnglerOptionModel[] anglers)
    {
        return new TripAssociationModel(TripId, "Corrib weekend", "Lough Corrib", Now.AddHours(-3),
            anglers.Length > 0 ? anglers : [new TripAnglerOptionModel(OwnerUserId, null, IsSelf: true)]);
    }

    private static LocalCatchRecordModel Record(
        Guid id, LocalCatchLifecycleEnum lifecycle, Guid? tripId, IReadOnlyList<LocalCatchPhotographModel>? photographs = null)
    {
        return new LocalCatchRecordModel
        {
            Id = id,
            UserId = OwnerUserId,
            TripId = tripId,
            CaughtByUserId = OwnerUserId,
            RecordedByUserId = OwnerUserId,
            CaughtOn = Now,
            Revision = 1,
            Lifecycle = lifecycle,
            CreatedOn = Now,
            UpdatedOn = Now,
            Photographs = photographs ?? []
        };
    }

    private void AddPhoto()
    {
        Sut.Photographs.SetItems(
        [
            new PhotographEditorItemModel
            {
                Id = Guid.NewGuid(), SortOrder = 0, PreviewPath = "p.jpg", RelativeFilePath = "p.jpg", ContentType = "image/jpeg"
            }
        ]);
    }

    private void ConfigureSave()
    {
        MockCatchStore.SaveAsync(Arg.Any<LocalCatchSaveArgs>(), Arg.Any<CancellationToken>())
            .Returns(call => LocalCatchSaveResultModel.Saved(call.Arg<LocalCatchSaveArgs>().Id, 1));
    }

    [Fact]
    public async Task ItShouldIgnoreAnAnglerWhoIsNotAParticipantOfTheTrip()
    {
        // Arrange
        MockTripAssociation.ResolveAsync(null, Arg.Any<CancellationToken>()).Returns(Trip(
            new TripAnglerOptionModel(OwnerUserId, null, IsSelf: true),
            new TripAnglerOptionModel(OtherAnglerId, "Sam", IsSelf: false)));
        await Sut.InitializeAsync(null, CancellationToken.None);
        // Act
        Sut.Trip.SelectAngler(Guid.NewGuid().ToString("D"));
        // Assert
        Sut.Trip.CaughtByUserId.Should().Be(OwnerUserId);
    }

    [Fact]
    public async Task ItShouldBlockSavingAndExplainWhenTheTripIsNoLongerOnTheDevice()
    {
        // Arrange
        MockTripAssociation.ResolveAsync(null, Arg.Any<CancellationToken>()).Returns(Trip());
        MockTripAssociation.IsAvailableAsync(TripId, Arg.Any<CancellationToken>()).Returns(false);
        await Sut.InitializeAsync(null, CancellationToken.None);
        AddPhoto();
        // Act
        var saved = await Sut.SaveAsync(CancellationToken.None);
        // Assert
        saved.Should().BeFalse();
        Sut.ValidationText.Should().Be(Text["CatchTripUnavailable"]);
        Sut.Trip.IsUnavailable.Should().BeTrue();
        await MockCatchStore.DidNotReceive().SaveAsync(Arg.Any<LocalCatchSaveArgs>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSurfaceADraftWhoseTripDisappearedAsUnavailable()
    {
        // Arrange
        MockTripAssociation.ResolveAsync(TripId, Arg.Any<CancellationToken>()).Returns((TripAssociationModel?)null);
        MockTripAssociation.IsAvailableAsync(TripId, Arg.Any<CancellationToken>()).Returns(false);
        MockCatchStore.ListAsync(Arg.Any<CancellationToken>()).Returns([Record(Guid.NewGuid(), LocalCatchLifecycleEnum.Draft, TripId)]);
        // Act
        await Sut.InitializeAsync(null, CancellationToken.None);
        AddPhoto();
        var saved = await Sut.SaveAsync(CancellationToken.None);
        // Assert
        Sut.Trip.CanLeave.Should().BeTrue();
        saved.Should().BeFalse();
        Sut.ValidationText.Should().Be(Text["CatchTripUnavailable"]);
    }

    [Fact]
    public async Task ItShouldKeepADraftWithoutATripStandaloneEvenWhenATripIsActive()
    {
        // Arrange
        MockTripAssociation.ResolveAsync(null, Arg.Any<CancellationToken>()).Returns(Trip());
        MockCatchStore.ListAsync(Arg.Any<CancellationToken>()).Returns([Record(Guid.NewGuid(), LocalCatchLifecycleEnum.Draft, null)]);
        // Act
        await Sut.InitializeAsync(null, CancellationToken.None);
        // Assert
        Sut.Trip.TripId.Should().BeNull();
        Sut.Trip.ShowStandalone.Should().BeTrue();
        Sut.Trip.CanRejoin.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldSaveStandaloneAfterLeavingAnUnavailableTrip()
    {
        // Arrange
        ConfigureSave();
        MockTripAssociation.ResolveAsync(null, Arg.Any<CancellationToken>()).Returns(Trip());
        MockTripAssociation.IsAvailableAsync(TripId, Arg.Any<CancellationToken>()).Returns(false);
        await Sut.InitializeAsync(null, CancellationToken.None);
        AddPhoto();
        await Sut.SaveAsync(CancellationToken.None);
        // Act
        Sut.Trip.Leave();
        var saved = await Sut.SaveAsync(CancellationToken.None);
        // Assert
        saved.Should().BeTrue();
        await MockCatchStore.Received(1).SaveAsync(
            Arg.Is<LocalCatchSaveArgs>(args => args.TripId == null && args.TripTitle == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldSaveWithoutATripWhenNoTripIsActive()
    {
        // Arrange
        ConfigureSave();
        await Sut.InitializeAsync(null, CancellationToken.None);
        AddPhoto();
        // Act
        var saved = await Sut.SaveAsync(CancellationToken.None);
        // Assert
        saved.Should().BeTrue();
        Sut.Trip.ShowAssociation.Should().BeFalse();
        Sut.Trip.ShowStandalone.Should().BeFalse();
        await MockCatchStore.Received(1).SaveAsync(
            Arg.Is<LocalCatchSaveArgs>(args => args.TripId == null && args.CaughtByUserId == OwnerUserId),
            Arg.Any<CancellationToken>());
        await MockTripAssociation.DidNotReceive().IsAvailableAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRequestTheTripPassedToTheEditor()
    {
        // Arrange
        MockTripAssociation.ResolveAsync(TripId, Arg.Any<CancellationToken>()).Returns(Trip());
        // Act
        await Sut.InitializeAsync(null, CancellationToken.None, TripId);
        // Assert
        Sut.Trip.TripId.Should().Be(TripId);
        await MockTripAssociation.Received(1).ResolveAsync(TripId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRestoreTheTripOfADraft()
    {
        // Arrange
        MockTripAssociation.ResolveAsync(TripId, Arg.Any<CancellationToken>()).Returns(Trip());
        MockCatchStore.ListAsync(Arg.Any<CancellationToken>()).Returns([Record(Guid.NewGuid(), LocalCatchLifecycleEnum.Draft, TripId)]);
        // Act
        await Sut.InitializeAsync(null, CancellationToken.None);
        // Assert
        Sut.Trip.TripId.Should().Be(TripId);
        Sut.Trip.TripName.Should().Be("Corrib weekend");
    }

    [Fact]
    public async Task ItShouldKeepTheTripOnAnExistingTripCatchWithoutOfferingToLeaveIt()
    {
        // Arrange
        var id = Guid.NewGuid();
        MockTripAssociation.ResolveAsync(TripId, Arg.Any<CancellationToken>()).Returns(Trip());
        MockCatchStore.GetAsync(id, Arg.Any<CancellationToken>()).Returns(Record(id, LocalCatchLifecycleEnum.Saved, TripId,
        [
            new LocalCatchPhotographModel
            {
                Id = Guid.NewGuid(), CatchId = id, UserId = OwnerUserId, SortOrder = 0, LocalFilePath = "a.jpg",
                ContentType = "image/jpeg", CreatedOn = Now
            }
        ]));
        ConfigureSave();
        // Act
        await Sut.InitializeAsync(id, CancellationToken.None);
        Sut.Trip.Leave();
        var saved = await Sut.SaveAsync(CancellationToken.None);
        // Assert
        Sut.Trip.CanLeave.Should().BeFalse();
        saved.Should().BeTrue();
        await MockCatchStore.Received(1).SaveAsync(
            Arg.Is<LocalCatchSaveArgs>(args => args.Id == id && args.TripId == TripId && args.CaughtByUserId == OwnerUserId),
            Arg.Any<CancellationToken>());
        await MockTripAssociation.DidNotReceive().IsAvailableAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldLeaveAndRejoinTheTripBeforeSavingIt()
    {
        // Arrange
        ConfigureSave();
        MockTripAssociation.ResolveAsync(null, Arg.Any<CancellationToken>()).Returns(Trip());
        await Sut.InitializeAsync(null, CancellationToken.None);
        AddPhoto();
        // Act
        Sut.Trip.Leave();
        var standalone = (Sut.Trip.ShowAssociation, Sut.Trip.ShowStandalone, Sut.Trip.CanRejoin);
        Sut.Trip.Rejoin();
        var saved = await Sut.SaveAsync(CancellationToken.None);
        // Assert
        standalone.Should().Be((false, true, true));
        saved.Should().BeTrue();
        await MockCatchStore.Received(1).SaveAsync(
            Arg.Is<LocalCatchSaveArgs>(args => args.TripId == TripId && args.TripTitle == "Corrib weekend"
                && args.CaughtByUserId == OwnerUserId && args.RecordedByUserId == OwnerUserId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldRecordTheSelectedParticipantAsTheAnglerAndTheOwnerAsTheRecorder()
    {
        // Arrange
        ConfigureSave();
        MockTripAssociation.ResolveAsync(null, Arg.Any<CancellationToken>()).Returns(Trip(
            new TripAnglerOptionModel(OwnerUserId, null, IsSelf: true),
            new TripAnglerOptionModel(OtherAnglerId, "Sam", IsSelf: false)));
        await Sut.InitializeAsync(null, CancellationToken.None);
        AddPhoto();
        // Act
        Sut.Trip.SelectAngler(OtherAnglerId.ToString("D"));
        await Sut.SaveAsync(CancellationToken.None);
        // Assert
        Sut.AnglerOptions.Select(option => option.Name).Should().Equal(Text["CatchAnglerMe"], "Sam");
        await MockCatchStore.Received(1).SaveAsync(
            Arg.Is<LocalCatchSaveArgs>(args => args.TripId == TripId && args.CaughtByUserId == OtherAnglerId
                && args.RecordedByUserId == OwnerUserId),
            Arg.Any<CancellationToken>());
    }
}
