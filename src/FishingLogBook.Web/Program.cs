using FishingLogBook.Web;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Localization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiConfig = builder.Configuration.GetSection(ApiConfig.SectionName).Get<ApiConfig>() ?? new ApiConfig();
var diagnosticsConfig = builder.Configuration.GetSection(DiagnosticsClientConfig.SectionName).Get<DiagnosticsClientConfig>()
    ?? new DiagnosticsClientConfig();
if (builder.HostEnvironment.IsDevelopment())
{
    diagnosticsConfig.ShowInspector = true;
    if (string.Equals(diagnosticsConfig.MinimumPersistLevel, "Warning", StringComparison.OrdinalIgnoreCase))
    {
        diagnosticsConfig.MinimumPersistLevel = "Information";
    }
}

var apiBaseAddress = string.IsNullOrWhiteSpace(apiConfig.BaseUrl)
    ? builder.HostEnvironment.BaseAddress
    : apiConfig.BaseUrl;

builder.Services.AddFishingLogBookWeb(apiConfig, diagnosticsConfig, new Uri(apiBaseAddress));

var host = builder.Build();
await host.Services.GetRequiredService<ICultureService>().InitializeAsync();
await host.RunAsync();
