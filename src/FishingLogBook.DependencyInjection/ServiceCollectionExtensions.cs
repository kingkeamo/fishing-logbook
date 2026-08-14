using FishingLogBook.Application.Contracts;
using FishingLogBook.Application.SystemStatus;
using FishingLogBook.Application.TestCatches;
using FishingLogBook.Domain.Config;
using FishingLogBook.Infrastructure.Persistence;
using FishingLogBook.Infrastructure.Storage;
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
        services.AddScoped<TestCatchService>();

        return services;
    }

    public static IServiceCollection AddFishingLogBookInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IDbConnectionFactory>(sp =>
        {
            var resolvedConfiguration = sp.GetService<IConfiguration>() ?? configuration;
            var connectionString = resolvedConfiguration.GetConnectionString(PostgresConnectionName) ?? string.Empty;
            return new NpgsqlConnectionFactory(connectionString);
        });
        services.AddScoped<ISystemRepository, SystemRepository>();
        services.AddScoped<ITestCatchRepository, TestCatchRepository>();
        services.Configure<ObjectStorageConfig>(configuration.GetSection(ObjectStorageConfig.SectionName));
        services.AddSingleton<IObjectStorage, S3CompatibleObjectStorage>();

        return services;
    }
}
