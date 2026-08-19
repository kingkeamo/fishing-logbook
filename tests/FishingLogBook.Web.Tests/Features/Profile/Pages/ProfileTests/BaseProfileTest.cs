using Bunit;
using Bunit.TestDoubles;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Web.Common.Modals;
using FishingLogBook.Web.Features.Profile.Clients;
using FishingLogBook.Web.Features.Profile.Models;
using FishingLogBook.Web.Features.Profile.Offline;
using FishingLogBook.Web.Features.Profile.Offline.Stores;
using FishingLogBook.Web.Features.Profile.Providers;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Profile.Pages.ProfileTests;

public class BaseProfileTest
{
    protected static readonly Guid FlyMethodId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    protected static readonly Guid SpinningMethodId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    protected static readonly Guid BrownTroutSpeciesId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    protected static readonly Guid PikeSpeciesId = Guid.Parse("cccccccc-0000-0000-0000-000000000002");

    protected static BunitContext CreateContext(
        IProfileClient profileClient,
        IFishingPreferenceClient? fishingPreferenceClient = null,
        IModalService? modalService = null,
        IAnglerPreferencesStore? cache = null,
        IAnglerPreferencesProvider? anglerPreferences = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(profileClient);
        context.Services.AddSingleton(fishingPreferenceClient ?? QuietFishingPreferenceClient());
        context.Services.AddSingleton(modalService ?? QuietModalService());
        context.Services.AddSingleton(cache ?? Substitute.For<IAnglerPreferencesStore>());
        context.Services.AddSingleton(
            anglerPreferences ?? Substitute.For<IAnglerPreferencesProvider>());
        context.Services.AddSingleton(Substitute.For<ICultureService>());
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        var authorization = context.AddAuthorization();
        authorization.SetAuthorized("tester@example.test");
        return context;
    }

    protected static IFishingPreferenceClient QuietFishingPreferenceClient(
        FishingPreferencesDto? preferences = null,
        FishingCatalogueDto? catalogue = null)
    {
        var client = Substitute.For<IFishingPreferenceClient>();
        client.GetCatalogueAsync(Arg.Any<CancellationToken>())
            .Returns(catalogue ?? new FishingCatalogueDto([], []));
        client.GetPreferencesAsync(Arg.Any<CancellationToken>())
            .Returns(preferences ?? new FishingPreferencesDto([]));
        client.UpdatePreferencesAsync(Arg.Any<UpdateFishingPreferencesDto>(), Arg.Any<CancellationToken>())
            .Returns(call => SavedPreferences(call.ArgAt<UpdateFishingPreferencesDto>(0)));
        return client;
    }

    protected static IModalService QuietModalService()
    {
        return Substitute.For<IModalService>();
    }

    protected static FishingCatalogueDto SampleCatalogue()
    {
        return new FishingCatalogueDto(
            [
                new FishingMethodDto(FlyMethodId, "Fly", "Fly"),
                new FishingMethodDto(SpinningMethodId, "Spinning", "Spinning")
            ],
            [
                new SpeciesDto(BrownTroutSpeciesId, "BrownTrout", "Brown Trout"),
                new SpeciesDto(PikeSpeciesId, "Pike", "Pike")
            ]);
    }

    protected static FishingPreferencesDto SamplePreferences()
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

    private static FishingPreferencesDto SavedPreferences(UpdateFishingPreferencesDto update)
    {
        var catalogue = SampleCatalogue();
        return new FishingPreferencesDto(
        [
            .. update.Methods.Select(method =>
            {
                var known = catalogue.Methods.First(item => item.Id == method.FishingMethodId);
                return new FishingMethodPreferenceDto(
                    known.Id,
                    known.Code,
                    known.Name,
                    method.IsDefault,
                    [
                        .. method.Species.Select(species =>
                        {
                            var knownSpecies = catalogue.AllSpecies.First(item => item.Id == species.SpeciesId);
                            return new FishingSpeciesPreferenceDto(
                                knownSpecies.Id,
                                knownSpecies.Code,
                                knownSpecies.Name,
                                species.IsDefault);
                        })
                    ]);
            })
        ]);
    }

    protected static ProfileDto EmptyProfile(Guid? userId = null)
    {
        return new ProfileDto(
            userId ?? Guid.NewGuid(),
            null,
            null,
            null,
            null,
            null,
            [],
            [],
            true,
            false,
            false,
            false,
            false);
    }

    protected static ProfileDto ToSaved(Guid userId, UpdateProfileDto update)
    {
        return new ProfileDto(
            userId,
            update.DisplayName,
            null,
            null,
            null,
            update.HomeRegion,
            update.PreferredFishingTypes,
            update.PreferredSpecies,
            update.ShowDisplayName,
            update.ShowPhotograph,
            update.ShowHomeRegion,
            update.ShowPreferredFishingTypes,
            update.ShowPreferredSpecies,
            update.PreferredWeightUnit,
            update.PreferredLengthUnit);
    }
}
