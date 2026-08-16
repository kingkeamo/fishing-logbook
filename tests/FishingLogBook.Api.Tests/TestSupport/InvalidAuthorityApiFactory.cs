using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace FishingLogBook.Api.Tests.TestSupport;

public sealed class InvalidAuthorityApiFactory : WebApplicationFactory<Program>
{
    private readonly string _authority;

    public InvalidAuthorityApiFactory(string authority)
    {
        _authority = authority;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            var values = new Dictionary<string, string?>(TestAuthentication.Configuration)
            {
                ["ConnectionStrings:Postgres"] =
                    "Host=127.0.0.1;Database=fishing-logbook-auth-test;Username=test;Password=test",
                ["Auth:Authority"] = _authority
            };
            configuration.AddInMemoryCollection(values);
        });
    }
}
