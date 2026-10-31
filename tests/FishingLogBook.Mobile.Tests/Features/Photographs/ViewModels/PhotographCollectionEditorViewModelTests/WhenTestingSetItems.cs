using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Photographs.Enums;
using FishingLogBook.Mobile.Features.Photographs.Models;
using FishingLogBook.Mobile.Features.Photographs.Services;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Photographs.ViewModels.PhotographCollectionEditorViewModelTests;

public sealed class WhenTestingSetItems : BasePhotographCollectionEditorViewModelTest
{
    [Fact]
    public void ItShouldOrderItemsAndExposeAddRemoveLabels()
    {
        // Arrange
        var later = Photo(2);
        var earlier = Photo(0);
        var middle = Photo(1);
        var changed = 0;
        Sut.ItemsChanged += (_, _) => changed++;

        // Act
        Sut.SetItems([later, earlier, middle]);

        // Assert
        Sut.Items.Select(item => item.Id).Should().Equal(earlier.Id, middle.Id, later.Id);
        Sut.HasItems.Should().BeTrue();
        Sut.CanAddMore.Should().BeTrue();
        Sut.CanRemove.Should().BeTrue();
        Sut.HasStatus.Should().BeFalse();
        changed.Should().Be(1);
        Sut.Text.Should().BeSameAs(Text);
        Sut.MaximumCount.Should().Be(10);
    }

    [Fact]
    public async Task ItShouldClearAFailureStatusWhenItemsAreReplaced()
    {
        // Arrange
        MockEditor.TakePhotoAsync(
                OwnerUserId,
                Arg.Any<IReadOnlyList<PhotographEditorItemModel>>(),
                10,
                Arg.Any<CancellationToken>())
            .Returns(PhotographEditorActionResultModel.Unchanged(
                [],
                PhotographPreparationOutcomeEnum.PermissionDenied));
        await Sut.TakePhotoAsync(CancellationToken.None);
        Sut.HasStatus.Should().BeTrue();

        // Act
        Sut.SetItems([Photo()]);

        // Assert
        Sut.HasStatus.Should().BeFalse();
        Sut.StatusText.Should().BeEmpty();
        Sut.HasItems.Should().BeTrue();
    }

    [Fact]
    public void ItShouldStopAddingWhenTheMaximumIsReached()
    {
        // Arrange
        Sut.MaximumCount = 1;

        // Act
        Sut.SetItems([Photo(), Photo(1)]);

        // Assert
        Sut.CanAddMore.Should().BeFalse();
        Sut.Items.Should().HaveCount(2);
    }

    [Fact]
    public void ItShouldIgnoreUnchangedUserIdAndMaximum()
    {
        // Arrange
        var raised = 0;
        Sut.PropertyChanged += (_, _) => raised++;

        // Act
        Sut.UserId = OwnerUserId;
        Sut.MaximumCount = 10;

        // Assert
        raised.Should().Be(0);
        Sut.UserId.Should().Be(OwnerUserId);
        Sut.MaximumCount.Should().Be(10);
    }
}
