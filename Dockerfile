# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore as distinct layers
COPY ["src/PadelApi/PadelApi.csproj", "padel-api/"]
RUN dotnet restore "padel-api/PadelApi.csproj"

# Copy everything else and build
COPY src/PadelApi/ padel-api/
WORKDIR /src/padel-api
RUN dotnet publish -c Release -o /app/out

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/out .

# Non-root user for security
USER $APP_UID

# Default port for .NET 8 containers is 8080
# Render sets PORT env var, which Program.cs uses to override binding if needed
ENV ASPNETCORE_HTTP_PORTS=8080

ENTRYPOINT ["dotnet", "PadelApi.dll"]
