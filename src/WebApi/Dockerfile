FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app

# for this build to work, you have to build this image from the solution dir
# this is because copy can't access parent dirs
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY . .
RUN pwd && ls -la
RUN dotnet restore "./src/WebApi/WebApi.csproj"
RUN dotnet build "./src/WebApi/WebApi.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./src/WebApi/WebApi.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final

# Install Tailscale (run as root for simplicity - Tailscale needs proper permissions)
USER root
RUN apt-get update && \
    apt-get install -y curl && \
    curl -fsSL https://tailscale.com/install.sh | sh && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Create standard Tailscale directories
RUN mkdir -p /var/lib/tailscale /var/run/tailscale && \
    chmod 755 /var/lib/tailscale /var/run/tailscale

WORKDIR /app
COPY --from=publish /app/publish .

# Copy entrypoint script
COPY src/WebApi/docker-entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

# Keep as root - Tailscale needs it, and we'll drop privileges in the app if needed

EXPOSE 8080
ENTRYPOINT ["/entrypoint.sh"]
# don't forget to map to a host port
