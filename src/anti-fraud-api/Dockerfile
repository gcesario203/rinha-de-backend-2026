# STAGE 1: Build / Publish usando o SDK do .NET 10
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia apenas os csproj primeiro para aproveitar cache das layers
COPY ["src/anti-fraud-api/anti-fraud-api.csproj", "src/anti-fraud-api/"]
COPY ["src/anti-fraud-core/anti-fraud-core.csproj", "src/anti-fraud-core/"]
COPY ["src/anti-fraud-application/anti-fraud-application.csproj", "src/anti-fraud-application/"]
COPY ["src/anti-fraud-infrastructure/anti-fraud-infrastructure.csproj", "src/anti-fraud-infrastructure/"]

RUN dotnet restore "src/anti-fraud-api/anti-fraud-api.csproj"

# Copia todo o código e publica em Release
COPY . .
WORKDIR "/src/src/anti-fraud-api"
RUN dotnet publish "anti-fraud-api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# STAGE 2: Runtime (imagem menor contendo apenas o ASP.NET runtime)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Instala 'curl' para permitir healthchecks simples via CMD curl
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Copia a publicação do estágio de build
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 80

ENTRYPOINT ["dotnet", "anti-fraud-api.dll"]