using FishingLogBook.Domain.Config;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Grafana.Loki;

namespace FishingLogBook.Api.Logging;

public static class SerilogHostLogging
{
    public static void Configure(LoggerConfiguration loggerConfiguration, IConfiguration configuration)
    {
        var externalLogging = configuration.GetSection(ExternalLoggingConfig.SectionName).Get<ExternalLoggingConfig>()
            ?? new ExternalLoggingConfig();

        loggerConfiguration
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("env", externalLogging.StreamEnvironment)
            .WriteTo.Console();

        if (!externalLogging.IsConfigured)
        {
            return;
        }

        loggerConfiguration.WriteTo.GrafanaLoki(
            externalLogging.LokiBaseUrl,
            labels:
            [
                new LokiLabel { Key = "app", Value = "fishing-logbook-api" },
                new LokiLabel { Key = "env", Value = externalLogging.StreamEnvironment }
            ],
            credentials: new LokiCredentials
            {
                Login = externalLogging.User,
                Password = externalLogging.ApiToken
            },
            restrictedToMinimumLevel: LogEventLevel.Information);
    }
}
