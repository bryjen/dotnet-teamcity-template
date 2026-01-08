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

# Install Tailscale (requires root during install, then run app as non-root)
USER root
RUN apt-get update && \
    apt-get install -y curl && \
    curl -fsSL https://tailscale.com/install.sh | sh && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=publish /app/publish .

# Copy entrypoint script that starts Tailscale (if configured) then the app
COPY src/WebApi/docker-entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

# Create directories Tailscale might need (though we use /tmp at runtime)
RUN mkdir -p /tmp/tailscale /tmp/tailscale-run /tmp/tailscale-lib && \
    chmod 777 /tmp/tailscale /tmp/tailscale-run /tmp/tailscale-lib

# Back to app user for running the service
USER $APP_UID

EXPOSE 8080
ENTRYPOINT ["/entrypoint.sh"]
# don't forget to map to a host port
