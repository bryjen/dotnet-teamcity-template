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
COPY --from=build /app/publish/wwwroot ./
COPY ./src/WebFrontend/nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 8080


