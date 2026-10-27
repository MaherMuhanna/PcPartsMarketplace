# Multi-stage build: SDK image compiles, small ASP.NET runtime image runs.
# Build context is the repository root (see docker-compose.yml).

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (cached layer as long as project files do not change).
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY PCM.DomainEntities/PCM.DomainEntities.csproj PCM.DomainEntities/
COPY PCM.Repositories/PCM.Repositories.csproj PCM.Repositories/
COPY PCM.Infrastructure/PCM.Infrastructure.csproj PCM.Infrastructure/
COPY PCM.API/PCM.API.csproj PCM.API/
RUN dotnet restore PCM.API/PCM.API.csproj

COPY PCM.DomainEntities/ PCM.DomainEntities/
COPY PCM.Repositories/ PCM.Repositories/
COPY PCM.Infrastructure/ PCM.Infrastructure/
COPY PCM.API/ PCM.API/
RUN dotnet publish PCM.API/PCM.API.csproj --configuration Release --no-restore --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
# Serilog writes daily files to /app/logs; the non-root app user needs write access.
RUN mkdir -p /app/logs && chown $APP_UID /app/logs
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "PCM.API.dll"]
