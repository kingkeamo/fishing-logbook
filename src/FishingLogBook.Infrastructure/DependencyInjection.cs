using FishingLogBook.Application.Contracts;
using FishingLogBook.Infrastructure.Migrations;
using FishingLogBook.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FishingLogBook.Infrastructure;

public static class DependencyInjection
{
    public const string PostgresConnectionName = "Postgres";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(PostgresConnectionName) ?? string.Empty;

        services.AddSingleton<IDbConnectionFactory>(_ => new NpgsqlConnectionFactory(connectionString));
        services.AddScoped<ISystemRepository, SystemRepository>();
        services.AddSingleton<IDatabaseMigrator>(serviceProvider =>
            new DbUpDatabaseMigrator(connectionString, serviceProvider.GetRequiredService<ILogger<DbUpDatabaseMigrator>>()));

        return services;
    }
}
