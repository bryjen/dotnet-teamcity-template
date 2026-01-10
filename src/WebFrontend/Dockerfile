FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
ARG API_BASE_URL=http://localhost:8080/

WORKDIR /src

# Copy from solution root (build context should be the solution directory)
COPY . .

# Set the API base URL used by the browser app (baked into the published static files)
RUN echo "Setting Api:BaseUrl to ${API_BASE_URL}" \
    && sed -i "s#\"BaseUrl\"\\s*:\\s*\"[^\"]*\"#\"BaseUrl\": \"${API_BASE_URL}\"#g" src/WebFrontend/wwwroot/appsettings.json

RUN dotnet restore "./src/WebFrontend/WebFrontend.csproj"
RUN dotnet publish "./src/WebFrontend/WebFrontend.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Serve the published wwwroot via nginx
FROM nginx:alpine AS final
WORKDIR /usr/share/nginx/html

# Copy published files
COPY --from=build /app/publish/wwwroot ./

# Remove development configuration file if present
RUN rm -f appsettings.Development.json || true

# Copy nginx config
COPY ./src/WebFrontend/nginx.conf /etc/nginx/conf.d/default.conf

# Copy and set up entrypoint script for runtime config replacement
COPY ./src/WebFrontend/docker-entrypoint.sh /docker-entrypoint.sh
RUN chmod +x /docker-entrypoint.sh

EXPOSE 8080

# Use custom entrypoint that replaces config values at runtime
ENTRYPOINT ["/docker-entrypoint.sh"]


