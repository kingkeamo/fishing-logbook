using FishingLogBook.Application.Capabilities.Services;
using FishingLogBook.Application.Catches.Services;
using FishingLogBook.Application.Common.Behaviours;
using FishingLogBook.Application.Common.Mappings;
using FishingLogBook.Application.Contracts;
using FishingLogBook.Application.Contracts.Repositories;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Application.Diagnostics;
using FishingLogBook.Application.Profiles.Services;
using FishingLogBook.Application.SystemStatus;
using FishingLogBook.Application.TestCatches;
using FishingLogBook.Application.Users;
using FishingLogBook.Application.Users.Commands;
using FishingLogBook.Application.Users.Services;
using FishingLogBook.Domain.Config;
using FishingLogBook.Infrastructure.Logging;
using FishingLogBook.Infrastructure.Persistence;
using FishingLogBook.Infrastructure.Storage;
using FluentValidation;
using Mapster;
using MapsterMapper;
using MediatR;
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
        var applicationAssembly = typeof(ResolveCurrentUserCommand).Assembly;
        services.AddMediatR(configuration => configuration.RegisterServicesFromAssembly(applicationAssembly));
        services.AddValidatorsFromAssembly(applicationAssembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
        services.AddFishingLogBookMappings();
        services.AddScoped<SystemStatusService>();
        services.AddScoped<TestCatchService>();
        services.AddScoped<DiagnosticLogService>();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<IUserIdentityService, UserIdentityService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<ICatchService, CatchService>();
        services.AddScoped<ICatchPhotographService, CatchPhotographService>();
        services.AddScoped<ICatchLocationPrivacyService, CatchLocationPrivacyService>();
        services.AddScoped<IPlatformCapabilityService, PlatformCapabilityService>();

        return services;
    }

    private static void AddFishingLogBookMappings(this IServiceCollection services)
    {
        var typeAdapterConfig = TypeAdapterConfig.GlobalSettings;
        typeAdapterConfig.Scan(typeof(UserMappingRegistration).Assembly);
        services.AddSingleton<IMapper>(new Mapper(typeAdapterConfig));
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
        services.AddScoped<IUserIdentityRepository, UserIdentityRepository>();
        services.AddScoped<IProfileRepository, ProfileRepository>();
        services.AddScoped<ICatchRepository, CatchRepository>();
        services.AddScoped<IUserPlatformCapabilityRepository, UserPlatformCapabilityRepository>();
        services.Configure<ObjectStorageConfig>(configuration.GetSection(ObjectStorageConfig.SectionName));
        services.Configure<DiagnosticsConfig>(configuration.GetSection(DiagnosticsConfig.SectionName));
        services.AddSingleton<IObjectStorage, S3CompatibleObjectStorage>();
        services.AddSingleton<IDiagnosticEventDeduplicator, InMemoryDiagnosticEventDeduplicator>();

        return services;
    }
}
