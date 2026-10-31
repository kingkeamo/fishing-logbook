using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Authentication.Models;
using FishingLogBook.Mobile.Features.Catch.Offline.Enums;
using FishingLogBook.Mobile.Features.Catch.Offline.Models;
using FishingLogBook.Mobile.Features.Photographs.Models;
using FishingLogBook.Shared.Photographs.Models;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Catch.ViewModels.CatchEditViewModelTests;

public class WhenTestingValidationAndSave : BaseCatchEditViewModelTest
{
    [Fact]
    public async Task ItShouldResolveConflictingPhotosUsingOnlyTheExplicitlyChosenPhotoDetails()
    {
        // Arrange
        await Sut.InitializeAsync(null, CancellationToken.None);
        var first = new PhotographEditorItemModel
        {
            Id = Guid.NewGuid(),
            SortOrder = 0,
            PreviewPath = "first.jpg",
            RelativeFilePath = "first.jpg",
            ContentType = "image/jpeg",
            Metadata = new PhotographMetadataModel(Now.AddDays(-2), 53.3, -6.2)
        };
        var second = new PhotographEditorItemModel
        {
            Id = Guid.NewGuid(),
            SortOrder = 1,
            PreviewPath = "second.jpg",
            RelativeFilePath = "second.jpg",
            ContentType = "image/jpeg",
            Metadata = new PhotographMetadataModel(Now.AddDays(-1), 54.5, -8.1)
        };
        Sut.Photographs.SetItems([first, second]);
        Sut.CanSave.Should().BeFalse();

        // Act
        Sut.Photographs.SelectPhotograph(first.Id);
        Sut.Photographs.UseCurrentDetails();
        Sut.Photographs.SelectPhotograph(second.Id);

        // Assert
        Sut.CanSave.Should().BeTrue();
        Sut.CaughtOnLocal.Should().Be(Now.AddDays(-2).UtcDateTime);
        Sut.LocationCaption.Should().Contain("53.3");
        Sut.LocationCaption.Should().Contain("Private");
        Sut.Photographs.AppliedPhotographId.Should().Be(first.Id);
        Sut.Photographs.IsCurrentDetailsApplied.Should().BeFalse();
        await MockCatchStore.DidNotReceive().SaveAsync(Arg.Any<LocalCatchSaveArgs>(), Arg.Any<CancellationToken>());
        Sut.Dispose();
    }

    [Fact]
    public async Task ItShouldRejectSaveWithoutPhotos()
    {
        // Arrange
        await Sut.InitializeAsync(null, CancellationToken.None);
        // Act
        var saved = await Sut.SaveAsync(CancellationToken.None);
        // Assert
        saved.Should().BeFalse();
        Sut.ValidationKey.Should().Be("CatchSaveRequiresPhoto");
        await MockCatchStore.DidNotReceive().SaveAsync(Arg.Any<LocalCatchSaveArgs>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldEnableSaveAndNotifyAfterAddingTheRequiredPhotoFollowingValidationFailure()
    {
        // Arrange
        await Sut.InitializeAsync(null, CancellationToken.None);
        Sut.CanSave.Should().BeFalse();
        Sut.ValidationText.Should().Be(Text["CatchSaveRequiresPhoto"]);
        await Sut.SaveAsync(CancellationToken.None);
        var notifications = new List<string?>();
        Sut.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        // Act
        Sut.Photographs.SetItems(
        [
            new PhotographEditorItemModel
            {
                Id = Guid.NewGuid(),
                SortOrder = 0,
                PreviewPath = "photo.jpg",
                RelativeFilePath = "photo.jpg",
                ContentType = "image/jpeg"
            }
        ]);

        // Assert
        Sut.CanSave.Should().BeTrue();
        Sut.HasValidation.Should().BeFalse();
        notifications.Should().Contain(nameof(Sut.CanSave));
        notifications.Should().Contain(nameof(Sut.ValidationText));
        await MockCatchStore.DidNotReceive().SaveAsync(Arg.Any<LocalCatchSaveArgs>(), Arg.Any<CancellationToken>());
        Sut.Dispose();
    }

    [Fact]
    public async Task ItShouldClearTextValidationAfterTheUserCorrectsNotes()
    {
        // Arrange
        await Sut.InitializeAsync(null, CancellationToken.None);
        Sut.Photographs.SetItems(
        [
            new PhotographEditorItemModel
            {
                Id = Guid.NewGuid(), SortOrder = 0, PreviewPath = "photo.jpg",
                RelativeFilePath = "photo.jpg", ContentType = "image/jpeg"
            }
        ]);
        Sut.Notes = new string('n', 2001);
        Sut.CanSave.Should().BeFalse();
        await Sut.SaveAsync(CancellationToken.None);

        // Act
        Sut.Notes = "Corrected";

        // Assert
        Sut.CanSave.Should().BeTrue();
        Sut.HasValidation.Should().BeFalse();
        await MockCatchStore.DidNotReceive().SaveAsync(Arg.Any<LocalCatchSaveArgs>(), Arg.Any<CancellationToken>());
        Sut.Dispose();
    }

    [Fact]
    public async Task ItShouldBlockSaveWhilePhotoDatesConflict()
    {
        // Arrange
        await Sut.InitializeAsync(null, CancellationToken.None);
        Sut.Photographs.SetItems(
        [
            new PhotographEditorItemModel
            {
                Id = Guid.NewGuid(),
                SortOrder = 0,
                PreviewPath = "a.jpg",
                RelativeFilePath = "a.jpg",
                ContentType = "image/jpeg",
                Metadata = new PhotographMetadataModel(DateTimeOffset.Parse("2026-06-01T10:00:00Z"), null, null)
            },
            new PhotographEditorItemModel
            {
                Id = Guid.NewGuid(),
                SortOrder = 1,
                PreviewPath = "b.jpg",
                RelativeFilePath = "b.jpg",
                ContentType = "image/jpeg",
                Metadata = new PhotographMetadataModel(DateTimeOffset.Parse("2026-06-02T10:00:00Z"), null, null)
            }
        ]);
        // Act
        var canSave = Sut.CanSave;
        // Assert
        canSave.Should().BeFalse();
        Sut.ShowDateConflict.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldPersistSavedCatchLocally()
    {
        // Arrange
        MockCatchStore.SaveAsync(Arg.Any<LocalCatchSaveArgs>(), Arg.Any<CancellationToken>())
            .Returns(LocalCatchSaveResultModel.Saved(Guid.NewGuid(), 1));
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
        // Act
        var saved = await Sut.SaveAsync(CancellationToken.None);
        // Assert
        saved.Should().BeTrue();
        await MockCatchStore.Received(1).SaveAsync(
            Arg.Is<LocalCatchSaveArgs>(args => args.Lifecycle == LocalCatchLifecycleEnum.Saved),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItShouldMarkNotSignedInWhenSessionIsMissing()
    {
        // Arrange
        MockSessionCoordinator.Capture().Returns(_ => (AccountSessionLeaseModel?)null);
        // Act
        await Sut.InitializeAsync(null, CancellationToken.None);
        // Assert
        Sut.NotSignedIn.Should().BeTrue();
    }

    [Fact]
    public async Task ItShouldNotSaveEditorContentsIntoADifferentAccountAfterSwitching()
    {
        // Arrange
        await Sut.InitializeAsync(null, CancellationToken.None);
        Sut.Photographs.SetItems(
        [
            new PhotographEditorItemModel
            {
                Id = Guid.NewGuid(), SortOrder = 0, PreviewPath = "photo.jpg",
                RelativeFilePath = $"{OwnerUserId:N}/photo.jpg", ContentType = "image/jpeg"
            }
        ]);
        MockSessionCoordinator.Capture().Returns(new AccountSessionLeaseModel(Guid.NewGuid(), 2, CancellationToken.None));

        // Act
        var saved = await Sut.SaveAsync(CancellationToken.None);

        // Assert
        saved.Should().BeFalse();
        Sut.NotSignedIn.Should().BeTrue();
        await MockCatchStore.DidNotReceive().SaveAsync(Arg.Any<LocalCatchSaveArgs>(), Arg.Any<CancellationToken>());
        Sut.Dispose();
    }
}
