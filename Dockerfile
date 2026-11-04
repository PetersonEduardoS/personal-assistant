# Build stage: restore, run the tests and publish the Api.
# If any test fails, the image is not built and nothing is deployed.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, so the packages layer is cached between builds
COPY PersonalAssistant.slnx ./
COPY src/PersonalAssistant.Api/PersonalAssistant.Api.csproj src/PersonalAssistant.Api/
COPY src/PersonalAssistant.Core/PersonalAssistant.Core.csproj src/PersonalAssistant.Core/
COPY src/PersonalAssistant.Infrastructure/PersonalAssistant.Infrastructure.csproj src/PersonalAssistant.Infrastructure/
COPY tests/PersonalAssistant.Core.Tests/PersonalAssistant.Core.Tests.csproj tests/PersonalAssistant.Core.Tests/
COPY tests/PersonalAssistant.Infrastructure.Tests/PersonalAssistant.Infrastructure.Tests.csproj tests/PersonalAssistant.Infrastructure.Tests/
COPY tests/PersonalAssistant.Api.Tests/PersonalAssistant.Api.Tests.csproj tests/PersonalAssistant.Api.Tests/
RUN dotnet restore PersonalAssistant.slnx

COPY . .
RUN dotnet test PersonalAssistant.slnx -c Release --no-restore
RUN dotnet publish src/PersonalAssistant.Api/PersonalAssistant.Api.csproj -c Release --no-restore -o /app/publish

# Runtime stage: only the ASP.NET Core runtime and the published app
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Folder for the database and the context files (a volume is mounted here in production)
RUN mkdir -p /data/contexts && chown -R $APP_UID /data

COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
ENV Database__Path=/data/personal-assistant.db
EXPOSE 8080

USER $APP_UID
ENTRYPOINT ["dotnet", "PersonalAssistant.Api.dll"]
