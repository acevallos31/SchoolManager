FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY SchoolManager.API.csproj .
RUN dotnet restore SchoolManager.API.csproj

COPY . .
RUN dotnet publish SchoolManager.API.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["sh", "-c", "dotnet SchoolManager.API.dll --urls http://0.0.0.0:${PORT:-8080}"]
