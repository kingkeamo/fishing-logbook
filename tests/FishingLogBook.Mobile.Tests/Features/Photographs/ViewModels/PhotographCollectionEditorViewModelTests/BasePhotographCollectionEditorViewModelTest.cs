using System.Globalization;
using FishingLogBook.Mobile.Features.Photographs.Models;
using FishingLogBook.Mobile.Features.Photographs.Services;
using FishingLogBook.Mobile.Features.Photographs.ViewModels;
using FishingLogBook.Mobile.Localization;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Photographs.ViewModels.PhotographCollectionEditorViewModelTests;

public abstract class BasePhotographCollectionEditorViewModelTest
{
    protected static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    protected UiTextService Text { get; } = new(CultureInfo.GetCultureInfo("en-GB"));
    protected IPhotographCollectionEditorService MockEditor { get; } = Substitute.For<IPhotographCollectionEditorService>();
    protected PhotographCollectionEditorViewModel Sut { get; }

    protected BasePhotographCollectionEditorViewModelTest()
    {
        MockEditor.DefaultMaximumCount.Returns(10);
        Sut = new PhotographCollectionEditorViewModel(MockEditor, Text);
        Sut.UserId = OwnerUserId;
    }

    protected static PhotographEditorItemModel Photo(int sortOrder = 0, Guid? id = null)
    {
        return new PhotographEditorItemModel
        {
            Id = id ?? Guid.NewGuid(),
            SortOrder = sortOrder,
            PreviewPath = $"p{sortOrder}.jpg",
            RelativeFilePath = $"p{sortOrder}.jpg",
            ContentType = "image/jpeg"
        };
    }
}
