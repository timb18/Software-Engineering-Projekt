# ===== Base Image =====
# Use official .NET runtime as base image for final application
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base

# Create a non-root user for security best practices
ARG APP_UID=10001
ARG APP_GID=10001
RUN groupadd --gid $APP_GID appgroup \
    && useradd --uid $APP_UID --gid $APP_GID --create-home --shell /usr/sbin/nologin appuser

# Run application as non-root user
USER $APP_UID:$APP_GID
WORKDIR /app

# Expose port 8080 for incoming HTTP requests
EXPOSE 8080

# ===== Build Stage =====
# Use .NET SDK to build the application
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files and restore NuGet dependencies
# This layer is cached separately for faster rebuilds when only source changes
COPY ["Api/Api.csproj", "Api/"]
COPY ["Services/Services.csproj", "Services/"]
COPY ["DataAccess/DataAccess.csproj", "DataAccess/"]
RUN dotnet restore "Api/Api.csproj"

# Copy remaining source code and build the application
COPY . .
RUN dotnet build "Api/Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

# ===== Publish Stage =====
# Publish the application to remove unnecessary files and optimize for production
FROM build AS publish

ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Api/Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# ===== Final Stage =====
# Copy published application to base runtime image
FROM base AS final
WORKDIR /app

# Copy compiled application from publish stage
COPY --from=publish /app/publish .

# Start the application
ENTRYPOINT ["dotnet", "Api.dll"]
