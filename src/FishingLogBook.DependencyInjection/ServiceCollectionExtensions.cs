using FishingLogBook.Application.Contracts;
using FishingLogBook.Application.SystemStatus;
using FishingLogBook.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FishingLogBook.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public const string PostgresConnectionName = "Postgres";

    public static IServiceCollection AddFishingLogBook(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddFishingLogBookApplication();
        services.AddFishingLogBookInfrastructure(configuration);

        return services;
    }

    public static IServiceCollection AddFishingLogBookApplication(this IServiceCollection services)
    {
        services.AddScoped<SystemStatusService>();

        return services;
    }

    public static IServiceCollection AddFishingLogBookInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(PostgresConnectionName) ?? string.Empty;

        services.AddSingleton<IDbConnectionFactory>(_ => new NpgsqlConnectionFactory(connectionString));
        services.AddScoped<ISystemRepository, SystemRepository>();

        return services;
    }
}
