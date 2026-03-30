# ── Build stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore dependencies first (layer-cached until .csproj changes)
COPY ["src/LightningLab3/LightningLab3.csproj", "src/LightningLab3/"]
RUN dotnet restore "src/LightningLab3/LightningLab3.csproj"

# Copy source and publish
COPY . .
WORKDIR "/src/src/LightningLab3"
RUN dotnet publish -c Release -o /app/publish --no-restore

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "LightningLab3.dll"]
