using System.Diagnostics.CodeAnalysis;
using DbUp.Engine;
using FishingLogBook.Db.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;

namespace FishingLogBook.Db.Migrations.App;

[ExcludeFromCodeCoverage]
internal static class Program
{
    private const int ExitSuccess = 0;
    private const int ExitFailure = 1;

    public static int Main(string[] args)
    {
        var configuration = GetConfiguration();

        CreateLogger(configuration);

        Log.Information("Starting up");

        try
        {
            var connectionString = configuration["Db:ConnectionString"];
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                Log.Fatal("No connection string configured. Set 'Db:ConnectionString' via appsettings, user secrets, or the 'Db__ConnectionString' environment variable.");
                return ExitFailure;
            }

            WriteBanner($"Current connection: {MaskConnectionString(connectionString)}");

            using var loggerFactory = LoggerFactory.Create(loggingBuilder => loggingBuilder.AddSerilog(Log.Logger));
            var logger = loggerFactory.CreateLogger<MigrationService>();
            var migrationService = new MigrationService(logger);

            if (!migrationService.DatabaseExists(connectionString))
            {
                return ExitFailure;
            }

            // Scripts are sorted by FILENAME ONLY (not folder path) so the YYYYMMDDHHMM prefix
            // gives true chronological ordering across the numbered migration folders.
            var migrationsAssembly = typeof(MigrationService).Assembly;
            var upgradeEngine = migrationService.CreateUpgradeEngine(connectionString, migrationsAssembly);

            var scripts = upgradeEngine.GetScriptsToExecute();

            if (scripts.Count == 0)
            {
                Console.WriteLine("No new migrations found. Nothing to run!");
                Console.WriteLine("Ensure all new SQL scripts are placed under the numbered migration folders so they are embedded for processing.");
                return ExitSuccess;
            }

            ShowScriptsToBeRun(scripts);

            if (ShouldRunNonInteractively(args))
            {
                return migrationService.RunMigrations(upgradeEngine) ? ExitSuccess : ExitFailure;
            }

            return RunInteractive(migrationService, upgradeEngine, scripts);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Unhandled exception");
            return ExitFailure;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static bool ShouldRunNonInteractively(string[] args)
    {
        // Allow unattended execution in CI/pipelines: an explicit flag, or when there is no
        // interactive console to prompt against (stdin redirected).
        var hasRunFlag = args.Any(arg =>
            arg.Equals("--run", StringComparison.OrdinalIgnoreCase) ||
            arg.Equals("--yes", StringComparison.OrdinalIgnoreCase) ||
            arg.Equals("-y", StringComparison.OrdinalIgnoreCase));

        return hasRunFlag || Console.IsInputRedirected;
    }

    private static int RunInteractive(MigrationService migrationService, UpgradeEngine upgradeEngine, IReadOnlyCollection<SqlScript> scripts)
    {
        var inputOptions = ShowOptions();

        while (inputOptions != "3")
        {
            switch (inputOptions)
            {
                case "1":
                    {
                        var succeeded = migrationService.RunMigrations(upgradeEngine);
                        if (succeeded)
                        {
                            WriteExitMessage();
                            return ExitSuccess;
                        }

                        return ExitFailure;
                    }
                case "2":
                    {
                        ShowSqlScriptsContent(scripts);
                        inputOptions = ShowOptions();
                        break;
                    }
                default:
                    {
                        inputOptions = ShowOptions();
                        break;
                    }
            }
        }

        return ExitSuccess;
    }

    private static void CreateLogger(IConfiguration configuration)
    {
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();
    }

    private static IConfiguration GetConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets(typeof(Program).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();
    }

    private static string MaskConnectionString(string connectionString)
    {
        try
        {
            var builder = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
            if (!string.IsNullOrEmpty(builder.Password))
            {
                builder.Password = "****";
            }

            return builder.ToString();
        }
        catch
        {
            return "(unparseable connection string)";
        }
    }

    private static void WriteBanner(string output)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("-------------------------------------------------------------------");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine(output);
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("-------------------------------------------------------------------");
        Console.WriteLine();
        Console.ResetColor();
    }

    private static string ShowOptions()
    {
        Console.WriteLine();
        Console.WriteLine("1: Run Migrations");
        Console.WriteLine("2: View all scripts.");
        Console.WriteLine("3: Cancel.");
        return Console.ReadLine() ?? string.Empty;
    }

    private static void WriteExitMessage()
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Completed!");
        Console.ResetColor();
    }

    private static void ShowSqlScriptsContent(IEnumerable<SqlScript> scriptList)
    {
        foreach (var sqlScript in scriptList)
        {
            WriteSeparator();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(sqlScript.Name);
            WriteSeparator();
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(sqlScript.Contents);
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++");
            Console.ResetColor();
        }
    }

    private static void ShowScriptsToBeRun(IEnumerable<SqlScript> scriptList)
    {
        Console.WriteLine();
        WriteSeparator();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("Following scripts will be executed");
        WriteSeparator();
        foreach (var sqlScript in scriptList)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(sqlScript.Name);
        }

        WriteSeparator();
        Console.ResetColor();
    }

    private static void WriteSeparator()
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("-------------------------------------------------------------------");
    }
}
