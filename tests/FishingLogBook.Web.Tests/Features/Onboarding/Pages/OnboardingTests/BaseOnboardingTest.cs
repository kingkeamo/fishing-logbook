using Bunit;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Browser.Install;
using FishingLogBook.Web.Browser.Location;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Diagnostics.Services;
using FishingLogBook.Web.Features.Onboarding.Services;
using FishingLogBook.Web.Features.Profile.Clients;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;
using OnboardingPage = FishingLogBook.Web.Features.Onboarding.Pages.Onboarding.Onboarding;

namespace FishingLogBook.Web.Tests.Features.Onboarding.Pages.OnboardingTests;

public class BaseOnboardingTest
{
    protected static readonly Guid FlyMethodId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    protected static readonly Guid SpinningMethodId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    protected static readonly Guid BrownTroutSpeciesId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");

    protected static async Task MoveToPreferencesAsync(IRenderedComponent<OnboardingPage> cut)
    {
        cut.WaitForAssertion(() => cut.Find("#onboarding-next"));
        await cut.Find("#onboarding-next").ClickAsync();
        cut.WaitForAssertion(() => cut.Find("#onboarding-method-chips"));
    }

    protected static FishingPreferencesDto ValidPreferences()
    {
        return new FishingPreferencesDto(
        [
            new FishingMethodPreferenceDto(
                FlyMethodId,
                "Fly",
                "Fly",
                true,
                [new FishingSpeciesPreferenceDto(BrownTroutSpeciesId, "BrownTrout", "Brown Trout", true)])
        ]);
    }

    protected static Fixture CreateFixture(
        FishingPreferencesDto savedPreferences,
        bool isCompleted = false,
        InstallState? installState = null,
        InstallResult installResult = InstallResult.Unavailable)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        context.Services.AddSingleton(Substitute.For<IModalService>());

        var onboarding = Substitute.For<IOnboardingService>();
        onboarding.IsCompletedAsync(Arg.Any<CancellationToken>()).Returns(isCompleted);
        context.Services.AddSingleton(onboarding);

        var profileDto = new ProfileDto(
            Guid.NewGuid(), null, null, null, null, null, true, false, false, false, false);
        var profile = Substitute.For<IProfileClient>();
        profile.GetOwnAsync(Arg.Any<CancellationToken>()).Returns(profileDto);
        profile.UpdateOwnAsync(Arg.Any<UpdateProfileDto>(), Arg.Any<CancellationToken>()).Returns(profileDto);
        context.Services.AddSingleton(profile);

        var preferences = Substitute.For<IFishingPreferenceClient>();
        preferences.GetCatalogueAsync(Arg.Any<CancellationToken>()).Returns(new FishingCatalogueDto(
            [
                new FishingMethodDto(FlyMethodId, "Fly", "Fly"),
                new FishingMethodDto(SpinningMethodId, "Spinning", "Spinning")
            ],
            [new SpeciesDto(BrownTroutSpeciesId, "BrownTrout", "Brown Trout")]));
        preferences.GetPreferencesAsync(Arg.Any<CancellationToken>()).Returns(savedPreferences);
        preferences.UpdatePreferencesAsync(
                Arg.Any<UpdateFishingPreferencesDto>(), Arg.Any<CancellationToken>())
            .Returns(savedPreferences);
        context.Services.AddSingleton(preferences);

        var install = Substitute.For<IInstallService>();
        install.GetStateAsync(Arg.Any<CancellationToken>())
            .Returns(installState ?? InstallState.Unknown);
        install.PromptAsync(Arg.Any<CancellationToken>()).Returns(installResult);
        context.Services.AddSingleton(install);
        context.Services.AddSingleton(Substitute.For<ILocationService>());
        context.Services.AddSingleton(Substitute.For<ILoggingService>());
        context.AddAuthorization().SetAuthorized("tester@example.test");
        return new Fixture(context, onboarding, profile, preferences, install);
    }

    protected sealed record Fixture(
        BunitContext Context,
        IOnboardingService Onboarding,
        IProfileClient Profile,
        IFishingPreferenceClient Preferences,
        IInstallService Install) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }
}
