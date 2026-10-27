using System.Globalization;
using FishingLogBook.Mobile.Api.Enums;
using FishingLogBook.Mobile.Api.Models;
using FishingLogBook.Mobile.Features.Authentication.Services;
using FishingLogBook.Mobile.Features.Profile.Models;
using FishingLogBook.Mobile.Features.Profile.Services;
using FishingLogBook.Mobile.Features.Profile.ViewModels;
using FishingLogBook.Mobile.Localization;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Profile.ViewModels.AnglerPreferencesViewModelTests;

public abstract class BaseAnglerPreferencesViewModelTest
{
    protected readonly IAnglerPreferencesService MockService = Substitute.For<IAnglerPreferencesService>();
    protected readonly IAuthenticationService MockAuthenticationService = Substitute.For<IAuthenticationService>();
    protected readonly UiTextService Text = new(CultureInfo.GetCultureInfo("en-GB"));
    protected readonly AnglerPreferencesViewModel Sut;

    protected BaseAnglerPreferencesViewModelTest()
    {
        MockService.GetCachedAsync(Arg.Any<CancellationToken>()).Returns((AnglerPreferencesModel?)null);
        Sut = new AnglerPreferencesViewModel(MockService, MockAuthenticationService, new CatalogueTextService(Text), Text);
        Sut.Attach(action => action());
    }

    protected void RefreshReturns(AnglerPreferencesModel preferences)
    {
        MockService.RefreshAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<AnglerPreferencesModel>.Succeeded(preferences));
    }

    protected void RefreshFails(ApiOutcomeEnum outcome)
    {
        MockService.RefreshAsync(Arg.Any<CancellationToken>())
            .Returns(ApiResultModel<AnglerPreferencesModel>.Failed(outcome));
    }

    protected void ChangeAccount()
    {
        MockAuthenticationService.SessionChanged += Raise.Event();
    }
}
