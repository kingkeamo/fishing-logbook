# Multi-stage build for the FishingLogBook API.
# Provider-neutral: configuration is supplied via environment variables at runtime.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["FishingLogBook.sln", "Directory.Packages.props", "./"]
COPY ["src/FishingLogBook.Api/FishingLogBook.Api.csproj", "src/FishingLogBook.Api/"]
COPY ["src/FishingLogBook.Application/FishingLogBook.Application.csproj", "src/FishingLogBook.Application/"]
COPY ["src/FishingLogBook.DependencyInjection/FishingLogBook.DependencyInjection.csproj", "src/FishingLogBook.DependencyInjection/"]
COPY ["src/FishingLogBook.Domain/FishingLogBook.Domain.csproj", "src/FishingLogBook.Domain/"]
COPY ["src/FishingLogBook.Infrastructure/FishingLogBook.Infrastructure.csproj", "src/FishingLogBook.Infrastructure/"]
COPY ["src/FishingLogBook.Shared/FishingLogBook.Shared.csproj", "src/FishingLogBook.Shared/"]
RUN dotnet restore "src/FishingLogBook.Api/FishingLogBook.Api.csproj"

COPY src/ src/
RUN dotnet publish "src/FishingLogBook.Api/FishingLogBook.Api.csproj" \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .

USER $APP_UID
ENTRYPOINT ["dotnet", "FishingLogBook.Api.dll"]
