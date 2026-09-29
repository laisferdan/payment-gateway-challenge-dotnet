# Build: restore first (cached until the project file changes), then publish.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source
COPY src/PaymentGateway.Api/PaymentGateway.Api.csproj src/PaymentGateway.Api/
RUN dotnet restore src/PaymentGateway.Api/PaymentGateway.Api.csproj
COPY src/ src/
RUN dotnet publish src/PaymentGateway.Api/PaymentGateway.Api.csproj -c Release -o /app --no-restore /p:UseAppHost=false

# Runtime: ASP.NET Core only, non-root, nothing else installed (not even a shell – "chiseled").
# GET /health is for the orchestrator's HTTP probe, which needs no tool inside the image.
FROM mcr.microsoft.com/dotnet/aspnet:8.0-noble-chiseled AS final
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "PaymentGateway.Api.dll"]
