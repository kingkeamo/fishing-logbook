using FishingLogBook.Application.SystemStatus;
using Microsoft.Extensions.DependencyInjection;

namespace FishingLogBook.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<SystemStatusService>();

        return services;
    }
}
