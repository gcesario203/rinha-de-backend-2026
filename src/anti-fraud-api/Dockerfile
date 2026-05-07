# STAGE 1: Build / Publish usando o SDK do .NET 10
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia apenas os csproj primeiro para aproveitar cache das layers
COPY ["anti-fraud-api/anti-fraud-api.csproj", "anti-fraud-api/"]
COPY ["anti-fraud-core/anti-fraud-core.csproj", "anti-fraud-core/"]
COPY ["anti-fraud-application/anti-fraud-application.csproj", "anti-fraud-application/"]
COPY ["anti-fraud-infrastructure/anti-fraud-infrastructure.csproj", "anti-fraud-infrastructure/"]

RUN dotnet restore "anti-fraud-api/anti-fraud-api.csproj"

# Copia todo o código e publica em Release
COPY . .
WORKDIR "/src/anti-fraud-api"
RUN dotnet publish "anti-fraud-api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# STAGE 2: Runtime (imagem menor contendo apenas o ASP.NET runtime)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Instala 'curl' para permitir healthchecks simples via CMD curl
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Copia a publicação do estágio de build
COPY --from=build /app/publish .

# Certifique-se de que os arquivos estáticos (ex: shared/resources/references.json.gz)
# estejam marcados no csproj do projeto infra como CopyToOutputDirectory=PreserveNewest,
# assim eles serão incluídos na pasta /app/publish automaticamente.

ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 80

# ATENÇÃO: altere o nome da DLL abaixo para o nome gerado pelo seu projeto
# Ex.: "anti-fraud-api.dll" ou "AntiFraud.API.dll"
ENTRYPOINT ["dotnet", "anti-fraud-api.dll"]