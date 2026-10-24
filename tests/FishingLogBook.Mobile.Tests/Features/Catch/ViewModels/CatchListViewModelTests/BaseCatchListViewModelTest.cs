using System.Globalization;
using FishingLogBook.Mobile.Features.Catch.ViewModels;
using FishingLogBook.Mobile.Localization;

namespace FishingLogBook.Mobile.Tests.Features.Catch.ViewModels.CatchListViewModelTests;

public abstract class BaseCatchListViewModelTest
{
    protected UiTextService Text { get; } = new(CultureInfo.GetCultureInfo("en-GB"));
    protected CatchListViewModel Sut { get; }

    protected BaseCatchListViewModelTest()
    {
        Sut = new CatchListViewModel(Text);
    }
}
