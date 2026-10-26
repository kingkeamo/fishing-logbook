using System.Globalization;
using FishingLogBook.Mobile.Features.Catch.Models;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Mobile.Navigation;
using FishingLogBook.Shared.Dtos;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Navigation.NavigationServiceTests;

public abstract class BaseNavigationServiceTest
{
    protected readonly IRouteNavigator MockNavigator = Substitute.For<IRouteNavigator>();
    protected readonly CatchPresentationModel Catch = new(new CatchDto(Guid.NewGuid(), DateTimeOffset.UtcNow, []), new UiTextService(CultureInfo.GetCultureInfo("en-GB")), "sample_catch.png");
    protected readonly NavigationService Sut;

    protected BaseNavigationServiceTest()
    {
        MockNavigator.CurrentPath.Returns("//main/catches/catches-root");
        Sut = new NavigationService(MockNavigator);
    }
}
