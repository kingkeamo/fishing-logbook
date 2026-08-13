using FishingLogBook.Application.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace FishingLogBook.Api.Tests;

public sealed class SystemApiFactory : WebApplicationFactory<Program>
{
    public ISystemRepository SystemRepository { get; } = Substitute.For<ISystemRepository>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = string.Empty
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISystemRepository>();
            services.AddScoped(_ => SystemRepository);
        });
    }
}
