# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["backend/src/Api/Api.csproj", "Api/"]
COPY ["backend/src/Application/Application.csproj", "Application/"]
COPY ["backend/src/Infrastructure/Infrastructure.csproj", "Infrastructure/"]
COPY ["backend/src/Contracts/Contracts.csproj", "Contracts/"]
RUN dotnet restore "Api/Api.csproj"

# Copy source code and build
COPY backend/src/ .
WORKDIR "/src/Api"
RUN dotnet build "Api.csproj" -c Release -o /app/build
RUN dotnet publish "Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Copy policy file (now accessible from parent context)
COPY db/policy/schema-policy.json /app/policy/schema-policy.json

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 5000

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Copy published app
COPY --from=build /app/publish .
COPY --from=build /app/policy ./policy

ENTRYPOINT ["dotnet", "Api.dll"]
