FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["backend/src/SeedRunner/SeedRunner.csproj", "SeedRunner/"]
RUN dotnet restore "SeedRunner/SeedRunner.csproj"

COPY backend/src/SeedRunner/ SeedRunner/
WORKDIR "/src/SeedRunner"
RUN dotnet publish "SeedRunner.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "SeedRunner.dll"]
