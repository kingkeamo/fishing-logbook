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
    public async Task ItShouldRejectSaveWithoutPhotos()
    {
        // Arrange
        await Sut.InitializeAsync(null, CancellationToken.None);
        // Act
        var saved = await Sut.SaveAsync(CancellationToken.None);
        // Assert
        saved.Should().BeFalse();
        Sut.ValidationKey.Should().Be("CatchSaveRequiresPhoto");
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
}
