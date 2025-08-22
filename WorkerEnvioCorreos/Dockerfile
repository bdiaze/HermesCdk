# Se efectúa build de la aplicación
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Se restauran las dependencias
COPY ["WorkerEnvioCorreos/WorkerEnvioCorreos.csproj", "WorkerEnvioCorreos/"]
RUN dotnet restore "WorkerEnvioCorreos/WorkerEnvioCorreos.csproj"

# Se publica el proyecto
COPY . .
WORKDIR "/src/WorkerEnvioCorreos"
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Se ejecuta runtime
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "WorkerEnvioCorreos.dll"]