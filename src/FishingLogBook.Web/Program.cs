using FishingLogBook.Web;
using FishingLogBook.Web.Configuration;
using FishingLogBook.Web.Localization;
using FishingLogBook.Web.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiConfig = builder.Configuration.GetSection(ApiConfig.SectionName).Get<ApiConfig>() ?? new ApiConfig();

var apiBaseAddress = string.IsNullOrWhiteSpace(apiConfig.BaseUrl)
    ? builder.HostEnvironment.BaseAddress
    : apiConfig.BaseUrl;

builder.Services.AddSingleton(apiConfig);
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(apiBaseAddress) });
builder.Services.AddScoped<ISystemStatusClient, SystemStatusClient>();
builder.Services.AddLocalization();
builder.Services.AddScoped<ICultureService, CultureService>();
builder.Services.AddMudServices();
builder.Services.AddTransient<MudLocalizer, FishingLogBookMudLocalizer>();

var host = builder.Build();
await host.Services.GetRequiredService<ICultureService>().InitializeAsync();
await host.RunAsync();
