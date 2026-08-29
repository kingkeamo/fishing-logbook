using System.Collections.Concurrent;
using System.Net.Http.Headers;
using FishingLogBook.Api.Configuration;
using FishingLogBook.Api.Tests.TestSupport;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Contracts;
using FishingLogBook.Application.Contracts.Repositories;
using FishingLogBook.Domain.Catalogue;
using FishingLogBook.Domain.Catches;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.FishingLocations;
using FishingLogBook.Domain.Profiles;
using FishingLogBook.Domain.Trips;
using FishingLogBook.Domain.Users;
using FishingLogBook.Tests.Common.TestSupport;
using FluentResults;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace FishingLogBook.Api.Tests;

public class SystemApiFactory : WebApplicationFactory<Program>
{
    private readonly ConcurrentDictionary<string, Guid> _userIds = new(StringComparer.Ordinal);

    public ISystemRepository SystemRepository { get; } = Substitute.For<ISystemRepository>();

    public IObjectStorage ObjectStorage { get; } = Substitute.For<IObjectStorage>();

    public IUserIdentityRepository UserIdentityRepository { get; } = Substitute.For<IUserIdentityRepository>();

    public IOfflineAccessPreferenceRepository OfflineAccessPreferenceRepository { get; } =
        Substitute.For<IOfflineAccessPreferenceRepository>();

    public IProfileRepository ProfileRepository { get; } = Substitute.For<IProfileRepository>();

    public ICatchRepository CatchRepository { get; } = Substitute.For<ICatchRepository>();

    public ITripRepository TripRepository { get; } = Substitute.For<ITripRepository>();

    public ITripParticipantRepository TripParticipantRepository { get; } =
        Substitute.For<ITripParticipantRepository>();

    public ITripPhotographRepository TripPhotographRepository { get; } =
        Substitute.For<ITripPhotographRepository>();

    public ITripNoteRepository TripNoteRepository { get; } = Substitute.For<ITripNoteRepository>();

    public IUserPlatformCapabilityRepository UserPlatformCapabilityRepository { get; } =
        Substitute.For<IUserPlatformCapabilityRepository>();

    public IFishingCatalogueRepository FishingCatalogueRepository { get; } =
        Substitute.For<IFishingCatalogueRepository>();

    public IFishingPreferenceRepository FishingPreferenceRepository { get; } =
        Substitute.For<IFishingPreferenceRepository>();

    public IFishingLocationPreferenceRepository FishingLocationPreferenceRepository { get; } =
        Substitute.For<IFishingLocationPreferenceRepository>();

    public static readonly Guid FlyMethodId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    public static readonly Guid SpinningMethodId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");

    public static readonly Guid BrownTroutSpeciesId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");

    public static readonly Guid PikeSpeciesId = Guid.Parse("cccccccc-0000-0000-0000-000000000002");

    public bool MappingFailed { get; set; }

    public SystemApiFactory()
    {
        UserIdentityRepository
            .FindUserIdAsync(Arg.Any<FindUserIdentityArgs>(), Arg.Any<CancellationToken>())
            .Returns(call => ResolveFind(call.ArgAt<FindUserIdentityArgs>(0).Subject));
        UserIdentityRepository
            .CreateAsync(Arg.Any<User>(), Arg.Any<UserIdentity>(), Arg.Any<CancellationToken>())
            .Returns(call => ResolveCreate(call.ArgAt<UserIdentity>(1).Subject));
        UserIdentityRepository
            .UpdateEmailAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok());
        OfflineAccessPreferenceRepository
            .GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(new FishingLogBook.Shared.Dtos.OfflineAccessPreferenceDto(false)));
        OfflineAccessPreferenceRepository
            .SetAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Ok(new FishingLogBook.Shared.Dtos.OfflineAccessPreferenceDto(
                call.ArgAt<bool>(1),
                call.ArgAt<bool>(1) ? DateTimeOffset.Parse("2026-08-23T12:00:00Z") : null)));
        ProfileRepository
            .UserExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(true));
        ProfileRepository
            .GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Profile?>(null));
        ProfileRepository
            .UpsertAsync(Arg.Any<Profile>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Ok(call.ArgAt<Profile>(0)));
        ProfileRepository
            .UpdatePhotographAsync(
                Arg.Any<RecordProfilePhotographArgs>(),
                Arg.Any<CancellationToken>())
            .Returns(call => Result.Ok(new Profile { UserId = call.ArgAt<RecordProfilePhotographArgs>(0).UserId }));
        ResetFishingCatalogue();
        ResetFishingPreferences();
        CatchRepository
            .UpsertAsync(Arg.Any<Catch>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Ok(call.ArgAt<Catch>(0)));
        CatchRepository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Catch?>(null));
        CatchRepository
            .UpdateLocationVisibilityAsync(Arg.Any<PersistCatchLocationVisibilityArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok());
        TripRepository
            .UpsertAsync(Arg.Any<Trip>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Ok(call.ArgAt<Trip>(0)));
        TripRepository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<Trip?>(null));
        TripRepository
            .GetSummariesForUserAsync(Arg.Any<GetMyTripsArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<TripSummary>>([]));
        TripRepository
            .GetCatchSummariesByTripIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<TripCatchSummary>>([]));
        TripNoteRepository
            .GetByTripIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<TripNote>>([]));
        TripPhotographRepository
            .GetByTripIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<TripPhotograph>>([]));
        TripParticipantRepository
            .FindAsync(Arg.Any<FindTripParticipantArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<TripParticipant?>(null));
        TripParticipantRepository
            .GetByTripIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<TripParticipant>>([]));
        TripParticipantRepository
            .GetPendingInvitationsByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<TripParticipant>>([]));
        TripParticipantRepository
            .UpsertAsync(Arg.Any<TripParticipant>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Ok(call.ArgAt<TripParticipant>(0)));
        ProfileRepository
            .GetByUserIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<Profile>>([]));
        ProfileRepository
            .FindAnglersAsync(Arg.Any<FindAnglersArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<AnglerSummary>>([]));
        UserPlatformCapabilityRepository
            .HasAsync(Arg.Any<FindUserPlatformCapabilityArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(false));
        UserPlatformCapabilityRepository
            .GetForUserAsync(Arg.Any<FindUserPlatformCapabilitiesArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<PlatformCapabilityEnum>>([]));
        UserPlatformCapabilityRepository
            .GrantAsync(Arg.Any<UserPlatformCapability>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok());
        UserPlatformCapabilityRepository
            .RevokeAsync(Arg.Any<FindUserPlatformCapabilityArgs>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok());
    }

    public void ResetFishingCatalogue()
    {
        FishingCatalogueRepository
            .GetAllMethodsAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<FishingMethod>>(
            [
                new FishingMethod { Id = FlyMethodId, Code = "Fly", Name = "Fly" },
                new FishingMethod { Id = SpinningMethodId, Code = "Spinning", Name = "Spinning" }
            ]));
        FishingCatalogueRepository
            .GetAllSpeciesAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<Species>>(
            [
                new Species { Id = BrownTroutSpeciesId, Code = "BrownTrout", Name = "Brown Trout" },
                new Species { Id = PikeSpeciesId, Code = "Pike", Name = "Pike" }
            ]));
    }

    public void ResetFishingPreferences()
    {
        FishingPreferenceRepository
            .GetMethodPreferencesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<UserFishingMethodPreference>>([]));
        FishingPreferenceRepository
            .GetSpeciesPreferencesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<UserFishingSpeciesPreference>>([]));
        FishingPreferenceRepository
            .ReplacePreferencesAsync(
                Arg.Any<Guid>(),
                Arg.Any<IReadOnlyList<UserFishingMethodPreference>>(),
                Arg.Any<IReadOnlyList<UserFishingSpeciesPreference>>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Ok());
    }

    public void ResetFishingLocations()
    {
        FishingLocationPreferenceRepository
            .GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok<IReadOnlyList<UserFishingLocationPreference>>([]));
        FishingLocationPreferenceRepository
            .ReplaceAsync(
                Arg.Any<Guid>(),
                Arg.Any<IReadOnlyList<UserFishingLocationPreference>>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Ok());
    }

    public HttpClient CreateAuthenticatedClient(string? accessToken = null)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken ?? TestJwt.CreateAccessToken());
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            var values = new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = string.Empty,
                ["Build:Version"] = "0.1.0",
                ["Build:Sha"] = "0123456789abcdef0123456789abcdef01234567",
                ["Build:Environment"] = "prod",
                ["Build:Timestamp"] = "2026-08-22T00:00:00Z"
            };
            foreach (var pair in TestAuthentication.Configuration)
            {
                values[pair.Key] = pair.Value;
            }

            configuration.AddInMemoryCollection(values);
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<BuildMetadataConfig>();
            services.AddSingleton(new BuildMetadataConfig
            {
                Version = "0.1.0",
                Sha = "0123456789abcdef0123456789abcdef01234567",
                Environment = "prod",
                Timestamp = DateTimeOffset.Parse("2026-08-22T00:00:00Z")
            });
            services.RemoveAll<AuthConfig>();
            services.AddSingleton(new AuthConfig
            {
                Authority = TestJwt.Issuer,
                ClientId = TestJwt.ClientId,
                ApiScope = TestAuthConstants.ApiScope,
                ApiResource = TestAuthConstants.ApiResource
            });
            TestAuthentication.ConfigureJwtBearer(services);
            services.RemoveAll<ISystemRepository>();
            services.AddScoped(_ => SystemRepository);
            services.RemoveAll<IObjectStorage>();
            services.AddSingleton(_ => ObjectStorage);
            services.RemoveAll<IUserIdentityRepository>();
            services.AddSingleton(UserIdentityRepository);
            services.RemoveAll<IOfflineAccessPreferenceRepository>();
            services.AddSingleton(OfflineAccessPreferenceRepository);
            services.RemoveAll<IProfileRepository>();
            services.AddSingleton(ProfileRepository);
            services.RemoveAll<ICatchRepository>();
            services.AddSingleton(CatchRepository);
            services.RemoveAll<ITripRepository>();
            services.RemoveAll<ITripPhotographRepository>();
            services.RemoveAll<ITripNoteRepository>();
            services.RemoveAll<ITripParticipantRepository>();
            services.AddSingleton(TripRepository);
            services.AddSingleton(TripParticipantRepository);
            services.AddSingleton(TripPhotographRepository);
            services.AddSingleton(TripNoteRepository);
            services.RemoveAll<IFishingCatalogueRepository>();
            services.AddSingleton(FishingCatalogueRepository);
            services.RemoveAll<IFishingPreferenceRepository>();
            services.AddSingleton(FishingPreferenceRepository);
            services.RemoveAll<IFishingLocationPreferenceRepository>();
            services.AddSingleton(FishingLocationPreferenceRepository);
            services.RemoveAll<IUserPlatformCapabilityRepository>();
            services.AddSingleton(UserPlatformCapabilityRepository);
            ConfigureAdditionalTestServices(services);
        });
    }

    protected virtual void ConfigureAdditionalTestServices(IServiceCollection services)
    {
    }

    private Result<Guid?> ResolveFind(string subject)
    {
        if (MappingFailed)
        {
            return Result.Fail<Guid?>("Failed to resolve FishingLogBook user.");
        }

        if (_userIds.TryGetValue(subject, out var userId))
        {
            return Result.Ok<Guid?>(userId);
        }

        return Result.Ok<Guid?>(null);
    }

    private Result<Guid> ResolveCreate(string subject)
    {
        if (MappingFailed)
        {
            return Result.Fail<Guid>("Failed to resolve FishingLogBook user.");
        }

        return Result.Ok(_userIds.GetOrAdd(subject, _ => Guid.NewGuid()));
    }
}
