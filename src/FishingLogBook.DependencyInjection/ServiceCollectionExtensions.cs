using FishingLogBook.Application.Capabilities.Services;
using FishingLogBook.Application.Catches.Services;
using FishingLogBook.Application.Common.Behaviours;
using FishingLogBook.Application.Common.Mappings;
using FishingLogBook.Application.Contracts;
using FishingLogBook.Application.Contracts.Repositories;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Application.Diagnostics;
using FishingLogBook.Application.FishingLocations.Services;
using FishingLogBook.Application.FishingPreferences.Services;
using FishingLogBook.Application.OfflineAccess.Services;
using FishingLogBook.Application.Profiles.Services;
using FishingLogBook.Application.SystemStatus;
using FishingLogBook.Application.Trips.Services;
using FishingLogBook.Application.Users;
using FishingLogBook.Application.Users.Commands;
using FishingLogBook.Application.Users.Services;
using FishingLogBook.Domain.Config;
using FishingLogBook.Infrastructure.Logging;
using FishingLogBook.Infrastructure.Persistence;
using FishingLogBook.Infrastructure.Persistence.Repositories;
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
        services.AddScoped<DiagnosticLogService>();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<IUserIdentityService, UserIdentityService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<ICatchService, CatchService>();
        services.AddScoped<ITripService, TripService>();
        services.AddScoped<ITripDetailService, TripDetailService>();
        services.AddScoped<ITripPhotographService, TripPhotographService>();
        services.AddScoped<ITripNoteService, TripNoteService>();
        services.AddScoped<ITripCatchService, TripCatchService>();
        services.AddScoped<ICatchPhotographService, CatchPhotographService>();
        services.AddScoped<ICatchLocationPrivacyService, CatchLocationPrivacyService>();
        services.AddScoped<IPlatformCapabilityService, PlatformCapabilityService>();
        services.AddScoped<IFishingLocationPreferenceService, FishingLocationPreferenceService>();
        services.AddScoped<IFishingPreferenceService, FishingPreferenceService>();
        services.AddScoped<IOfflineAccessPreferenceService, OfflineAccessPreferenceService>();

        return services;
    }

    private static void AddFishingLogBookMappings(this IServiceCollection services)
    {
        var typeAdapterConfig = new TypeAdapterConfig();
        typeAdapterConfig.Scan(typeof(CatchMappingRegistration).Assembly, typeof(CatchRepository).Assembly);
        services.AddSingleton(typeAdapterConfig);
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
        services.AddScoped<IUserIdentityRepository, UserIdentityRepository>();
        services.AddScoped<IProfileRepository, ProfileRepository>();
        services.AddScoped<ICatchRepository, CatchRepository>();
        services.AddScoped<ITripRepository, TripRepository>();
        services.AddScoped<ITripPhotographRepository, TripPhotographRepository>();
        services.AddScoped<ITripNoteRepository, TripNoteRepository>();
        services.AddScoped<IUserPlatformCapabilityRepository, UserPlatformCapabilityRepository>();
        services.AddScoped<IFishingCatalogueRepository, FishingCatalogueRepository>();
        services.AddScoped<IFishingLocationPreferenceRepository, FishingLocationPreferenceRepository>();
        services.AddScoped<IFishingPreferenceRepository, FishingPreferenceRepository>();
        services.AddScoped<IOfflineAccessPreferenceRepository, OfflineAccessPreferenceRepository>();
        services.Configure<ObjectStorageConfig>(configuration.GetSection(ObjectStorageConfig.SectionName));
        services.Configure<DiagnosticsConfig>(configuration.GetSection(DiagnosticsConfig.SectionName));
        services.AddSingleton<IObjectStorage, S3CompatibleObjectStorage>();
        services.AddSingleton<IDiagnosticEventDeduplicator, InMemoryDiagnosticEventDeduplicator>();

        return services;
    }
}
