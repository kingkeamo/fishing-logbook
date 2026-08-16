using FishingLogBook.Api.Authentication;
using FishingLogBook.Api.Configuration;
using FishingLogBook.Api.Endpoints;
using FishingLogBook.Api.Logging;
using FishingLogBook.Api.Middleware;
using FishingLogBook.DependencyInjection;
using FishingLogBook.Shared.Constants;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfiguration) =>
    SerilogHostLogging.Configure(loggerConfiguration, context.Configuration));

const string webClientCorsPolicy = "WebClient";

builder.Services.AddFishingLogBook(builder.Configuration);
builder.Services.AddOpenApi();

var authConfig = builder.Configuration.GetSection(AuthConfig.SectionName).Get<AuthConfig>() ?? new AuthConfig();
if (string.IsNullOrWhiteSpace(authConfig.ApiScope))
{
    authConfig.ApiScope = AuthConstants.ApiScope;
}

if (string.IsNullOrWhiteSpace(authConfig.ApiResource))
{
    authConfig.ApiResource = AuthConstants.DevApiResourceUri;
}

builder.Services.AddFishingLogBookJwtBearer(authConfig);

builder.Services.AddCors(options =>
{
    options.AddPolicy(webClientCorsPolicy, policy =>
    {
        var allowedOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? [];

        if (allowedOrigins.Length == 0)
        {
            policy.SetIsOriginAllowed(_ => false).AllowAnyHeader().AllowAnyMethod();
            return;
        }

        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

app.Logger.LogInformation(
    "FishingLogBook API starting in {HostingEnvironment} environment.",
    app.Environment.EnvironmentName);

app.MapOpenApi();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "FishingLogBook API");
});

if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors(webClientCorsPolicy);
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapSystemEndpoints();
app.MapTestCatchEndpoints();
app.MapDiagnosticEndpoints();

app.Run();

public partial class Program
{
}
