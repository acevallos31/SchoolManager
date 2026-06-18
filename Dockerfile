FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend/SchoolManager.API/SchoolManager.API.csproj backend/SchoolManager.API/
RUN dotnet restore backend/SchoolManager.API/SchoolManager.API.csproj

COPY backend/SchoolManager.API/ backend/SchoolManager.API/
RUN dotnet publish backend/SchoolManager.API/SchoolManager.API.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["sh", "-c", "dotnet SchoolManager.API.dll --urls http://0.0.0.0:${PORT:-8080}"]
