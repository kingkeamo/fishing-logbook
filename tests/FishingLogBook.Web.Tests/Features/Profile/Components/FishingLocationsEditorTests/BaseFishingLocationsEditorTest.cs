using Bunit;
using FishingLogBook.Web.Features.Profile.Models;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.Features.Profile.Components.FishingLocationsEditorTests;

public class BaseFishingLocationsEditorTest
{
    protected static readonly Guid CorribId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    protected static readonly Guid MoyId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");

    protected static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(Substitute.For<ICultureService>());
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();
        return context;
    }

    protected static List<FishingLocationEditModel> SavedLocations()
    {
        return
        [
            new FishingLocationEditModel(CorribId, "Lough Corrib", true),
            new FishingLocationEditModel(MoyId, "River Moy", false)
        ];
    }
}
