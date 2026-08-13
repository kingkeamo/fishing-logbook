using FishingLogBook.Api.Endpoints;
using FishingLogBook.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

const string webClientCorsPolicy = "WebClient";

builder.Services.AddFishingLogBook(builder.Configuration);

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

app.UseCors(webClientCorsPolicy);

app.MapSystemEndpoints();

app.Run();

public partial class Program
{
}
