using Bunit;
using FishingLogBook.Web.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace FishingLogBook.Web.Tests.Components.FishingLoaderTests;

public class BaseFishingLoaderTest
{
    protected static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLocalization();
        context.Services.AddTransient<MudBlazor.MudLocalizer, FishingLogBookMudLocalizer>();

        return context;
    }

    protected static string ReadComponentStylesheet()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FishingLogBook.sln")))
        {
            directory = directory.Parent;
        }

        return File.ReadAllText(Path.Combine(
            directory!.FullName,
            "src",
            "FishingLogBook.Web",
            "Components",
            "FishingLoader",
            "FishingLoader.razor.css"));
    }
}
