using System.Globalization;
using FishingLogBook.Mobile.Features.Authentication.Enums;
using FishingLogBook.Mobile.Features.Authentication.Services;
using FishingLogBook.Mobile.Features.Authentication.ViewModels;
using FishingLogBook.Mobile.Localization;
using FishingLogBook.Shared.Enums;
using NSubstitute;

namespace FishingLogBook.Mobile.Tests.Features.Authentication.ViewModels.AuthenticationViewModelTests;

public abstract class BaseAuthenticationViewModelTest
{
    protected readonly IAuthenticationService MockAuthenticationService = Substitute.For<IAuthenticationService>();
    protected readonly ISignInProviderService MockProviders = Substitute.For<ISignInProviderService>();
    protected readonly UiTextService Text = new(CultureInfo.GetCultureInfo("en-GB"));
    protected readonly AuthenticationViewModel Sut;
    protected int DispatchCount;

    protected BaseAuthenticationViewModelTest()
    {
        MockAuthenticationService.BeginSignInAsync(CancellationToken.None)
            .Returns(AuthenticationResultEnum.Pending);
        MockProviders.AvailableProviders.Returns([SignInProviderEnum.Apple, SignInProviderEnum.Google]);
        Sut = new AuthenticationViewModel(MockAuthenticationService, MockProviders, Text);
    }

    protected void Dispatch(Action action)
    {
        DispatchCount++;
        action();
    }

    protected void RaiseSignInCompleted(AuthenticationResultEnum result)
    {
        MockAuthenticationService.SignInCompleted +=
            Raise.Event<EventHandler<AuthenticationResultEnum>>(MockAuthenticationService, result);
    }
}
