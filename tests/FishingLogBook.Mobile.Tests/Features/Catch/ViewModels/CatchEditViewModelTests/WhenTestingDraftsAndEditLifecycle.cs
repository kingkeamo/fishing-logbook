using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Catch.Offline.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Models;
using FishingLogBook.Mobile.Features.Photographs.Models;
using FishingLogBook.Mobile.Tests.Support;
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
        Guid? photographId = null)
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
            Notes = notes,
            Revision = 1,
            Lifecycle = lifecycle,
            CreatedOn = Now,
            UpdatedOn = Now,
            Photographs = photographs
        };
    }
}
