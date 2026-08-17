using FishingLogBook.Api.Tests.TestSupport;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace FishingLogBook.Api.Tests;

public sealed class PlatformCapabilityApiFactory : SystemApiFactory
{
    protected override void ConfigureAdditionalTestServices(IServiceCollection services)
    {
        services.AddSingleton<IStartupFilter, TestGrantPlatformCapabilityStartupFilter>();
    }
}
