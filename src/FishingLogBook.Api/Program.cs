using FishingLogBook.Api.Endpoints;
using FishingLogBook.Api.Logging;
using FishingLogBook.Api.Middleware;
using FishingLogBook.DependencyInjection;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfiguration) =>
    SerilogHostLogging.Configure(loggerConfiguration, context.Configuration));

const string webClientCorsPolicy = "WebClient";

builder.Services.AddFishingLogBook(builder.Configuration);
builder.Services.AddOpenApi();

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
    "FishingLogBook API starting in {HostingEnvironment} environment.",
    app.Environment.EnvironmentName);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "FishingLogBook API");
    });
    app.UseHttpsRedirection();
}

app.UseCors(webClientCorsPolicy);
app.UseMiddleware<CorrelationIdMiddleware>();

app.MapSystemEndpoints();
app.MapTestCatchEndpoints();
app.MapDiagnosticEndpoints();

app.Run();

public partial class Program
{
}
