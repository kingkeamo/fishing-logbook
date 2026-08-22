# Multi-stage build for the FishingLogBook API.
# Provider-neutral: configuration is supplied via environment variables at runtime.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_VERSION=0.0.0-local
ARG BUILD_SHA=local
ARG BUILD_ENVIRONMENT=local
ARG BUILD_TIMESTAMP=1970-01-01T00:00:00Z
WORKDIR /src

RUN if [ "$BUILD_ENVIRONMENT" = "prod" ]; then \
      echo "$BUILD_VERSION" | grep -Eq '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$' && \
      echo "$BUILD_SHA" | grep -Eqi '^[0-9a-f]{40}$'; \
    fi

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
ARG BUILD_VERSION=0.0.0-local
ARG BUILD_SHA=local
ARG BUILD_ENVIRONMENT=local
ARG BUILD_TIMESTAMP=1970-01-01T00:00:00Z
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080
ENV Build__Version=$BUILD_VERSION
ENV Build__Sha=$BUILD_SHA
ENV Build__Environment=$BUILD_ENVIRONMENT
ENV Build__Timestamp=$BUILD_TIMESTAMP
EXPOSE 8080

COPY --from=build /app/publish .

USER $APP_UID
ENTRYPOINT ["dotnet", "FishingLogBook.Api.dll"]
