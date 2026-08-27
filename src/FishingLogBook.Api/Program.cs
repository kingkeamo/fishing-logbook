using FishingLogBook.Api.Authentication;
using FishingLogBook.Api.Configuration;
using FishingLogBook.Api.Endpoints;
using FishingLogBook.Api.Logging;
using FishingLogBook.Api.Middleware;
using FishingLogBook.DependencyInjection;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

var buildMetadata = builder.Configuration
    .GetSection(BuildMetadataConfig.SectionName)
    .Get<BuildMetadataConfig>() ?? new BuildMetadataConfig();
buildMetadata.EnsureRequired();

builder.Host.UseSerilog((context, loggerConfiguration) =>
    SerilogHostLogging.Configure(loggerConfiguration, context.Configuration));

const string webClientCorsPolicy = "WebClient";

builder.Services.AddFishingLogBook(builder.Configuration);
builder.Services.AddSingleton(buildMetadata);
builder.Services.AddOpenApi();
builder.Services.AddFishingLogBookJwtBearer(builder.Configuration);

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
_ = app.Services.GetRequiredService<AuthConfig>();

app.Logger.LogInformation(
    "FishingLogBook API {AppVersion} build {BuildSha} starting in {BuildEnvironment} environment.",
    buildMetadata.Version,
    buildMetadata.Sha,
    buildMetadata.Environment);

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
app.UseMiddleware<CurrentUserMiddleware>();

app.MapSystemEndpoints();
app.MapDiagnosticEndpoints();
app.MapUserEndpoints();
app.MapProfileEndpoints();
app.MapCatchEndpoints();
app.MapTripEndpoints();
app.MapFishingPreferenceEndpoints();
app.MapFishingLocationEndpoints();

app.Run();

public partial class Program
{
}
