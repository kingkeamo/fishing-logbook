using System.Globalization;
using AwesomeAssertions;
using FishingLogBook.Mobile.Features.Photographs.Models;
using FishingLogBook.Mobile.Features.Photographs.Services;
using FishingLogBook.Mobile.Features.Photographs.ViewModels;
using FishingLogBook.Mobile.Localization;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Photographs.ViewModels.PhotographCollectionEditorViewModelTests;

public class WhenTestingMinimumCount
{
    [Fact]
    public void ItShouldNotRemoveBelowTheConfiguredMinimum()
    {
        // Arrange
        var text = new UiTextService(CultureInfo.GetCultureInfo("en-GB"));
        var sut = new PhotographCollectionEditorViewModel(
            Substitute.For<IPhotographCollectionEditorService>(),
            text)
        {
            MinimumCount = 1
        };
        var only = new PhotographEditorItemModel
        {
            Id = Guid.NewGuid(),
            SortOrder = 0,
            PreviewPath = "a.jpg",
            RelativeFilePath = "a.jpg",
            ContentType = "image/jpeg"
        };
        sut.SetItems([only]);

        // Act
        sut.Remove(only.Id);

        // Assert
        sut.CanRemove.Should().BeFalse();
        sut.Items.Should().ContainSingle(item => item.Id == only.Id);
    }
}
