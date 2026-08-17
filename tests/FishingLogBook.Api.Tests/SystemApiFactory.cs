using System.Collections.Concurrent;
using System.Net.Http.Headers;
using FishingLogBook.Api.Configuration;
using FishingLogBook.Api.Tests.TestSupport;
using FishingLogBook.Application.Args;
using FishingLogBook.Application.Contracts;
using FishingLogBook.Application.Contracts.Repositories;
using FishingLogBook.Domain.Catches;
using FishingLogBook.Domain.Enums;
using FishingLogBook.Domain.Profiles;
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

    public ITestCatchRepository TestCatchRepository { get; } = Substitute.For<ITestCatchRepository>();

    public IObjectStorage ObjectStorage { get; } = Substitute.For<IObjectStorage>();

    public IUserIdentityRepository UserIdentityRepository { get; } = Substitute.For<IUserIdentityRepository>();

    public IProfileRepository ProfileRepository { get; } = Substitute.For<IProfileRepository>();

    public ICatchRepository CatchRepository { get; } = Substitute.For<ICatchRepository>();

    public IUserPlatformCapabilityRepository UserPlatformCapabilityRepository { get; } =
        Substitute.For<IUserPlatformCapabilityRepository>();

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
        CatchRepository
            .UpsertAsync(Arg.Any<Catch>(), Arg.Any<CancellationToken>())
            .Returns(call => Result.Ok(call.ArgAt<Catch>(0)));
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
                ["ConnectionStrings:Postgres"] = string.Empty
            };
            foreach (var pair in TestAuthentication.Configuration)
            {
                values[pair.Key] = pair.Value;
            }

            configuration.AddInMemoryCollection(values);
        });

        builder.ConfigureTestServices(services =>
        {
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
            services.RemoveAll<ITestCatchRepository>();
            services.AddScoped(_ => TestCatchRepository);
            services.RemoveAll<IObjectStorage>();
            services.AddSingleton(_ => ObjectStorage);
            services.RemoveAll<IUserIdentityRepository>();
            services.AddSingleton(UserIdentityRepository);
            services.RemoveAll<IProfileRepository>();
            services.AddSingleton(ProfileRepository);
            services.RemoveAll<ICatchRepository>();
            services.AddSingleton(CatchRepository);
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
