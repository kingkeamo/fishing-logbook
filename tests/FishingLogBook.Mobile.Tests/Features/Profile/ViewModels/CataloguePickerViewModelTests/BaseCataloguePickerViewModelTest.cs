using System.Globalization;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Features.Profile.ViewModels;
using FishingLogBook.Mobile.Localization;

namespace FishingLogBook.Mobile.Tests.Features.Profile.ViewModels.CataloguePickerViewModelTests;

public abstract class BaseCataloguePickerViewModelTest
{
    protected static readonly UiTextService Text = new(CultureInfo.GetCultureInfo("en-GB"));

    protected static CataloguePickerOptionModel Option(string name, bool isSelected = false)
    {
        return new CataloguePickerOptionModel(Guid.NewGuid(), name, "Remove " + name, isSelected);
    }

    protected static CataloguePickerOptionModel[] Options(int count)
    {
        return Enumerable.Range(1, count).Select(index => Option($"Species {index:00}")).ToArray();
    }

    protected static CataloguePickerViewModel CreateSut(params CataloguePickerOptionModel[] options)
    {
        return new CataloguePickerViewModel(Text, "Species", options);
    }
}
