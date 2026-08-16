using System.Collections;
using System.Reflection;
using AwesomeAssertions;
using FishingLogBook.Api.Logging;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace FishingLogBook.Api.Tests.SerilogHostLoggingTests;

public class WhenTestingConfigure
{
    [Fact]
    public void ItShouldNotAddLokiWhenGrafanaIsUnconfigured()
    {
        // Arrange
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ExternalLogging:Provider"] = "None",
            ["ExternalLogging:Url"] = "https://logs.example.test",
            ["ExternalLogging:ApiToken"] = "token"
        });
        var loggerConfiguration = new LoggerConfiguration();

        // Act
        SerilogHostLogging.Configure(loggerConfiguration, configuration);
        using var logger = loggerConfiguration.CreateLogger();

        // Assert
        SinkTypeNames(logger).Should().NotContain(name =>
            name.Contains("Loki", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ItShouldAddLokiWhenGrafanaIsConfigured()
    {
        // Arrange
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ExternalLogging:Provider"] = "GrafanaCloud",
            ["ExternalLogging:Url"] = "http://127.0.0.1:9/loki/api/v1/push",
            ["ExternalLogging:User"] = "12345",
            ["ExternalLogging:ApiToken"] = "token",
            ["ExternalLogging:Environment"] = "localhost"
        });
        var loggerConfiguration = new LoggerConfiguration();

        // Act
        SerilogHostLogging.Configure(loggerConfiguration, configuration);
        using var logger = loggerConfiguration.CreateLogger();

        // Assert
        SinkTypeNames(logger).Should().Contain(name =>
            name.Contains("Loki", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ItShouldAttachTheEnvProperty()
    {
        // Arrange
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["ExternalLogging:Provider"] = "None",
            ["ExternalLogging:Environment"] = "localhost"
        });
        var loggerConfiguration = new LoggerConfiguration();
        var mockSink = Substitute.For<ILogEventSink>();
        LogEvent? captured = null;
        mockSink.When(sink => sink.Emit(Arg.Any<LogEvent>())).Do(call => captured = call.Arg<LogEvent>());

        // Act
        SerilogHostLogging.Configure(loggerConfiguration, configuration);
        loggerConfiguration.WriteTo.Sink(mockSink);
        using var logger = loggerConfiguration.CreateLogger();
        logger.Information("started");

        // Assert
        captured.Should().NotBeNull();
        captured!.Properties.Should().ContainKey("env");
        captured.Properties["env"].Should().BeOfType<ScalarValue>().Which.Value.Should().Be("localhost");
        mockSink.Received(1).Emit(Arg.Is<LogEvent>(logEvent => logEvent.Properties.ContainsKey("env")));
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static IReadOnlyList<string> SinkTypeNames(ILogger logger)
    {
        var names = new List<string>();
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        Walk(logger, names, seen);
        return names;
    }

    private static void Walk(object? current, List<string> names, HashSet<object> seen)
    {
        if (current is null || current is string || !seen.Add(current))
        {
            return;
        }

        var type = current.GetType();
        var typeNamespace = type.Namespace ?? string.Empty;
        if (!typeNamespace.StartsWith("Serilog", StringComparison.Ordinal))
        {
            return;
        }

        names.Add(type.FullName ?? type.Name);

        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            var fieldValue = field.GetValue(current);
            if (fieldValue is IEnumerable enumerable and not string)
            {
                foreach (var item in enumerable)
                {
                    Walk(item, names, seen);
                }

                continue;
            }

            Walk(fieldValue, names, seen);
        }
    }
}
