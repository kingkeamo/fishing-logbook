using FishingLogBook.Api.Endpoints;
using FishingLogBook.Application;
using FishingLogBook.Application.Contracts;
using FishingLogBook.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

const string webClientCorsPolicy = "WebClient";

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddPolicy(webClientCorsPolicy, policy =>
    {
        var allowedOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? Array.Empty<string>();

        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
        }
        else
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
    });
});

var app = builder.Build();

app.Logger.LogInformation(
    "FishingLogBook API starting in {Environment} environment.",
    app.Environment.EnvironmentName);

RunStartupMigrations(app);

app.UseCors(webClientCorsPolicy);

app.MapSystemEndpoints();

app.Run();

static void RunStartupMigrations(WebApplication app)
{
    if (!app.Configuration.GetValue<bool>("Database:RunMigrationsOnStartup"))
    {
        return;
    }

    using var scope = app.Services.CreateScope();
    var migrator = scope.ServiceProvider.GetRequiredService<IDatabaseMigrator>();
    var result = migrator.Migrate();

    if (!result.Successful)
    {
        app.Logger.LogError(
            "Database migration did not complete successfully: {Error}",
            result.Error);
    }
}

public partial class Program
{
}
