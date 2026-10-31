using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Catch.Offline.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Models;
using FishingLogBook.Mobile.Features.Photographs.Models;
using FishingLogBook.Mobile.Features.Photographs.Services;
using FishingLogBook.Mobile.Tests.Support;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Catch.ViewModels.CatchEditViewModelTests;

public class WhenTestingDraftsAndEditLifecycle : BaseCatchEditViewModelTest
{
    [Fact]
    public async Task ItShouldRestoreTheMostRecentDraftWhenOpeningANewCatch()
    {
        // Arrange
        var draftId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        MockCatchStore.ListAsync(Arg.Any<CancellationToken>()).Returns(
        [
            CreateRecord(LocalCatchLifecycleEnum.Draft, draftId, notes: "Unfinished notes")
        ]);

        // Act
        await Sut.InitializeAsync(null, CancellationToken.None);

        // Assert
        Sut.DraftRestored.Should().BeTrue();
        Sut.ShowDraftRestored.Should().BeTrue();
        Sut.Notes.Should().Be("Unfinished notes");
        Sut.IsEditingSavedCatch.Should().BeFalse();
        Sut.Photographs.MinimumCount.Should().Be(0);
    }

    [Fact]
    public async Task ItShouldFlushDraftOnDisappearWithoutDemotingSavedCatches()
    {
        // Arrange
        await Sut.InitializeAsync(null, CancellationToken.None);
        Sut.Notes = "Halfway done";
        LocalCatchSaveArgs? draftArgs = null;
        MockCatchStore.SaveAsync(Arg.Any<LocalCatchSaveArgs>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                draftArgs = call.Arg<LocalCatchSaveArgs>();
                return LocalCatchSaveResultModel.Saved(draftArgs.Id, 1);
            });

        // Act
        await Sut.OnDisappearingAsync(CancellationToken.None);

        // Assert
        draftArgs.Should().NotBeNull();
        draftArgs!.Lifecycle.Should().Be(LocalCatchLifecycleEnum.Draft);
        draftArgs.Notes.Should().Be("Halfway done");
    }

    [Fact]
    public async Task ItShouldNotWriteDraftLifecycleWhenLeavingASavedCatchEdit()
    {
        // Arrange
        var catchId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var photoId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        MockCatchStore.GetAsync(catchId, Arg.Any<CancellationToken>()).Returns(
            CreateRecord(LocalCatchLifecycleEnum.Saved, catchId, photographId: photoId, notes: "Original"));
        await Sut.InitializeAsync(catchId, CancellationToken.None);
        Sut.Notes = "Edited notes";
        LocalCatchSaveArgs? savedArgs = null;
        MockCatchStore.SaveAsync(Arg.Any<LocalCatchSaveArgs>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                savedArgs = call.Arg<LocalCatchSaveArgs>();
                return LocalCatchSaveResultModel.Saved(savedArgs.Id, 2);
            });

        // Act
        await Sut.OnDisappearingAsync(CancellationToken.None);

        // Assert
        savedArgs.Should().NotBeNull();
        savedArgs!.Lifecycle.Should().Be(LocalCatchLifecycleEnum.Saved);
        savedArgs.Notes.Should().Be("Edited notes");
        Sut.IsSaved.Should().BeFalse();
        Sut.Photographs.MinimumCount.Should().Be(1);
    }

    [Fact]
    public async Task ItShouldRefuseRemovingTheLastPhotoOnASavedCatch()
    {
        // Arrange
        var catchId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var photoId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        MockCatchStore.GetAsync(catchId, Arg.Any<CancellationToken>()).Returns(
            CreateRecord(LocalCatchLifecycleEnum.Saved, catchId, photographId: photoId));
        await Sut.InitializeAsync(catchId, CancellationToken.None);

        // Act
        Sut.RemovePhoto(photoId);

        // Assert
        Sut.CanRemovePhoto.Should().BeFalse();
        Sut.Photographs.Items.Should().ContainSingle(item => item.Id == photoId);
        Sut.ValidationKey.Should().Be("CatchCannotRemoveLastPhoto");
        Sut.Photographs.CanRemove.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldTreatAnEmptyCatchIdAsANewCatch()
    {
        // Arrange
        MockPreferences.GetCachedAsync(Arg.Any<CancellationToken>()).Returns(TestAnglerPreferences.Create());

        // Act
        await Sut.InitializeAsync(Guid.Empty, CancellationToken.None);

        // Assert
        Sut.IsEditingSavedCatch.Should().BeFalse();
        Sut.PageTitle.Should().Be(Text["RecordCatch"]);
        await MockCatchStore.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldBindAnExistingCatchLocationWeightAndBait()
    {
        // Arrange
        var catchId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        MockCatchStore.GetAsync(catchId, Arg.Any<CancellationToken>()).Returns(
            CreateRecord(
                LocalCatchLifecycleEnum.Saved,
                catchId,
                notes: "bank",
                photographId: Guid.NewGuid(),
                bait: "nymph",
                weight: 2.2,
                length: 45,
                location: new LocalCatchLocationModel
                {
                    Latitude = 53.4,
                    Longitude = -6.2,
                    Source = LocationDefaults.PhotoMetadata,
                    Visibility = LocationDefaults.Private,
                    ConsentVersion = LocationDefaults.ConsentVersion,
                    CapturedOn = Now
                }));

        // Act
        await Sut.InitializeAsync(catchId, CancellationToken.None);

        // Assert
        Sut.IsEditingSavedCatch.Should().BeTrue();
        Sut.Notes.Should().Be("bank");
        Sut.BaitOrLure.Should().Be("nymph");
        Sut.WeightSummary.Should().Contain("2.2");
        Sut.LengthSummary.Should().Contain("45");
        Sut.HasLocation.Should().BeTrue();
        Sut.LocationCaption.Should().Contain("53.4");
    }

    [Fact]
    public async Task ItShouldRemoveAPhotoFromANewCatch()
    {
        // Arrange
        await Sut.InitializeAsync(null, CancellationToken.None);
        var photoId = Guid.NewGuid();
        Sut.Photographs.SetItems(
        [
            new PhotographEditorItemModel
            {
                Id = photoId,
                SortOrder = 0,
                PreviewPath = "a.jpg",
                RelativeFilePath = "a.jpg",
                ContentType = "image/jpeg"
            }
        ]);
        Sut.CanRemovePhoto.Should().BeTrue();
        MockPhotographEditor.Remove(Arg.Any<IReadOnlyList<PhotographEditorItemModel>>(), photoId)
            .Returns(PhotographEditorActionResultModel.Updated([]));

        // Act
        Sut.RemovePhoto(photoId);

        // Assert
        Sut.Photographs.Items.Should().BeEmpty();
        Sut.ShowCaughtOn.Should().BeFalse();
        Sut.CanSave.Should().BeFalse();
    }

    [Fact]
    public async Task ItShouldNotFlushAfterTheCatchIsSaved()
    {
        // Arrange
        MockCatchStore.SaveAsync(Arg.Any<LocalCatchSaveArgs>(), Arg.Any<CancellationToken>())
            .Returns(call => LocalCatchSaveResultModel.Saved(call.Arg<LocalCatchSaveArgs>().Id, 1));
        await Sut.InitializeAsync(null, CancellationToken.None);
        Sut.Photographs.SetItems(
        [
            new PhotographEditorItemModel
            {
                Id = Guid.NewGuid(),
                SortOrder = 0,
                PreviewPath = "a.jpg",
                RelativeFilePath = "a.jpg",
                ContentType = "image/jpeg"
            }
        ]);
        (await Sut.SaveAsync(CancellationToken.None)).Should().BeTrue();
        MockCatchStore.ClearReceivedCalls();

        // Act
        await Sut.OnDisappearingAsync(CancellationToken.None);

        // Assert
        await MockCatchStore.DidNotReceive().SaveAsync(Arg.Any<LocalCatchSaveArgs>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldNotFlushAnInvalidSavedCatchOnDisappear()
    {
        // Arrange
        var catchId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var photoId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        MockCatchStore.GetAsync(catchId, Arg.Any<CancellationToken>()).Returns(
            CreateRecord(LocalCatchLifecycleEnum.Saved, catchId, photographId: photoId));
        await Sut.InitializeAsync(catchId, CancellationToken.None);
        Sut.Notes = new string('n', 2001);
        MockCatchStore.ClearReceivedCalls();

        // Act
        await Sut.OnDisappearingAsync(CancellationToken.None);

        // Assert
        await MockCatchStore.DidNotReceive().SaveAsync(Arg.Any<LocalCatchSaveArgs>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldApplyPreferredMethodAndSpeciesDefaults()
    {
        // Arrange
        MockPreferences.GetCachedAsync(Arg.Any<CancellationToken>()).Returns(TestAnglerPreferences.Create());

        // Act
        await Sut.InitializeAsync(null, CancellationToken.None);

        // Assert
        Sut.MethodOptions.Should().Contain(option => option.Name == "Fly" && option.IsSelected);
        Sut.SpeciesOptions.Should().Contain(option => option.Name == "Salmon" && option.IsSelected);
    }

    private static LocalCatchRecordModel CreateRecord(
        LocalCatchLifecycleEnum lifecycle,
        Guid id,
        string? notes = null,
        Guid? photographId = null,
        string? bait = null,
        double? weight = null,
        double? length = null,
        LocalCatchLocationModel? location = null)
    {
        var photographs = photographId is null
            ? Array.Empty<LocalCatchPhotographModel>()
            : new[]
            {
                new LocalCatchPhotographModel
                {
                    Id = photographId.Value,
                    CatchId = id,
                    UserId = OwnerUserId,
                    SortOrder = 0,
                    LocalFilePath = "photos/a.jpg",
                    ContentType = "image/jpeg",
                    CreatedOn = Now
                }
            };

        return new LocalCatchRecordModel
        {
            Id = id,
            UserId = OwnerUserId,
            CaughtByUserId = OwnerUserId,
            RecordedByUserId = OwnerUserId,
            CaughtOn = Now,
            SpeciesName = "Brown trout",
            Method = "Fly",
            BaitOrLure = bait,
            Weight = weight,
            Length = length,
            Location = location,
            Notes = notes,
            Revision = 1,
            Lifecycle = lifecycle,
            CreatedOn = Now,
            UpdatedOn = Now,
            Photographs = photographs
        };
    }
}
