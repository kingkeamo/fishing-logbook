using Bunit;
using FishingLogBook.Web.Components;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NSubstitute;

namespace FishingLogBook.Web.Tests.LanguageSwitcherTests;

public class BaseLanguageSwitcherTest
{
    protected static BunitContext CreateContext(ICultureService cultureService)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddLocalization();
        context.Services.AddSingleton(cultureService);
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();

        return context;
    }
}
