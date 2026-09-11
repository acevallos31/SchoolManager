FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY SchoolManager.API.csproj .
RUN dotnet restore SchoolManager.API.csproj

COPY . .
RUN dotnet publish SchoolManager.API.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

# La imagen oficial de .NET incluye el usuario no privilegiado 'app'.
USER app

# El reverse proxy termina TLS; dentro de la red privada el contenedor recibe HTTP.
ENTRYPOINT exec dotnet SchoolManager.API.dll --urls "http://0.0.0.0:${PORT:-8080}" # NOSONAR
