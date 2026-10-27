# Multi-stage build for the MVC frontend. Build context is the repository root.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY PCM.DomainEntities/PCM.DomainEntities.csproj PCM.DomainEntities/
COPY PCM.Repositories/PCM.Repositories.csproj PCM.Repositories/
COPY PCM.Infrastructure/PCM.Infrastructure.csproj PCM.Infrastructure/
COPY PCM.UI/PCM.UI.csproj PCM.UI/
RUN dotnet restore PCM.UI/PCM.UI.csproj

COPY PCM.DomainEntities/ PCM.DomainEntities/
COPY PCM.Repositories/ PCM.Repositories/
COPY PCM.Infrastructure/ PCM.Infrastructure/
COPY PCM.UI/ PCM.UI/
RUN dotnet publish PCM.UI/PCM.UI.csproj --configuration Release --no-restore --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
RUN mkdir -p /app/logs && chown $APP_UID /app/logs
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "PCM.UI.dll"]
