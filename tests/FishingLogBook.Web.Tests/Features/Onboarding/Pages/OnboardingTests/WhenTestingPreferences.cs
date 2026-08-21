using AwesomeAssertions;
using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Browser.Install;
using FishingLogBook.Web.Browser.Location;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Onboarding.Services;
using FishingLogBook.Web.Features.Profile.Clients;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;
using OnboardingPage = FishingLogBook.Web.Features.Onboarding.Pages.Onboarding.Onboarding;

namespace FishingLogBook.Web.Tests.Features.Onboarding.Pages.OnboardingTests;

public class WhenTestingPreferences
{
    private static readonly Guid FlyMethodId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid SpinningMethodId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");

    [Fact]
    public async Task ItShouldUseTheReusableMultiSelectWithExistingMethodsSelected()
    {
        // Arrange
        using var culture = TestCulture.Use(CultureNames.English);
        var modal = Substitute.For<IModalService>();
        modal.ShowAsync<CataloguePickerModal, CataloguePickerModalModel, CataloguePickerModalResult>(
                Arg.Any<CataloguePickerModalModel>(), Arg.Any<CancellationToken>())
            .Returns(new CataloguePickerModalResult(
            [
                new CatalogueOptionModel(FlyMethodId, "Fly", "Fly"),
                new CatalogueOptionModel(SpinningMethodId, "Spinning", "Spinning")
            ]));
        await using var context = CreateContext(modal);
        var cut = context.Render<OnboardingPage>();
        cut.WaitForAssertion(() => cut.Find("#onboarding-next"));
        await cut.Find("#onboarding-next").ClickAsync();
        cut.WaitForAssertion(() => cut.Find("#onboarding-method-more"));

        // Act
        await cut.Find("#onboarding-method-more").ClickAsync();

        // Assert
        cut.WaitForAssertion(() =>
            cut.Find("#onboarding-method-Spinning").ClassList.Should().Contain("mud-chip-filled"));
        await modal.Received(1)
            .ShowAsync<CataloguePickerModal, CataloguePickerModalModel, CataloguePickerModalResult>(
                Arg.Is<CataloguePickerModalModel>(model =>
                    model.AllowMultiple
                    && model.Options.Count == 2
                    && model.SelectedOptionIds != null
                    && model.SelectedOptionIds.Contains(FlyMethodId)),
                Arg.Any<CancellationToken>());
    }

    private static BunitContext CreateContext(IModalService modal)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        context.Services.AddSingleton(modal);

        var onboarding = Substitute.For<IOnboardingService>();
        onboarding.IsCompletedAsync(Arg.Any<CancellationToken>()).Returns(false);
        context.Services.AddSingleton(onboarding);

        var profile = Substitute.For<IProfileClient>();
        profile.GetOwnAsync(Arg.Any<CancellationToken>()).Returns(new ProfileDto(
            Guid.NewGuid(), null, null, null, null, null, true, false, false, false, false));
        context.Services.AddSingleton(profile);

        var preferences = Substitute.For<IFishingPreferenceClient>();
        preferences.GetCatalogueAsync(Arg.Any<CancellationToken>()).Returns(new FishingCatalogueDto(
            [
                new FishingMethodDto(FlyMethodId, "Fly", "Fly"),
                new FishingMethodDto(SpinningMethodId, "Spinning", "Spinning")
            ],
            []));
        preferences.GetPreferencesAsync(Arg.Any<CancellationToken>()).Returns(new FishingPreferencesDto(
        [
            new FishingMethodPreferenceDto(FlyMethodId, "Fly", "Fly", true, [])
        ]));
        context.Services.AddSingleton(preferences);

        var install = Substitute.For<IInstallService>();
        install.GetStateAsync(Arg.Any<CancellationToken>()).Returns(new InstallState(false, false, false, false));
        context.Services.AddSingleton(install);
        context.Services.AddSingleton(Substitute.For<ILocationService>());
        context.AddAuthorization().SetAuthorized("tester@example.test");
        return context;
    }
}
