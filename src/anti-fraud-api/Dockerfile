# =============================================================================
# STAGE 1: Build + publish + prebuild dos artefactos (references.bin/.balltree.bin)
# =============================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore primeiro (cache de layer)
COPY ["src/anti-fraud-api/anti-fraud-api.csproj", "src/anti-fraud-api/"]
COPY ["src/anti-fraud-core/anti-fraud-core.csproj", "src/anti-fraud-core/"]
COPY ["src/anti-fraud-application/anti-fraud-application.csproj", "src/anti-fraud-application/"]
COPY ["src/anti-fraud-infrastructure/anti-fraud-infrastructure.csproj", "src/anti-fraud-infrastructure/"]
RUN dotnet restore "src/anti-fraud-api/anti-fraud-api.csproj"

# Código-fonte + recursos (inclui resources/references.json.gz)
COPY . .
WORKDIR "/src/src/anti-fraud-api"
RUN dotnet publish "anti-fraud-api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# -----------------------------------------------------------------------------
# Prebuild dos artefactos PESADOS (references.bin + references.balltree.bin)
# Executa o próprio binário publicado no modo --prebuild.
# Saída fica em /app/data e é copiada para a imagem final em /var/lib/antifraud.
# Esta camada só re-executa se references.json.gz ou o publish output mudarem.
# -----------------------------------------------------------------------------
RUN mkdir -p /app/data \
 && dotnet /app/publish/anti-fraud-api.dll --prebuild \
        /src/resources/references.json.gz \
        /app/data/references.bin \
        /app/data/references.balltree.bin \
        /app/data/references.kdtree.bin \
 && rm -f /app/publish/shared/resources/references.json.gz \
 && ls -lh /app/data

# =============================================================================
# STAGE 2: Runtime enxuto (ASP.NET runtime + dados pré-construídos)
# =============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Dados pré-construídos durante o `docker build`
RUN mkdir -p /var/lib/antifraud
COPY --from=build /app/data/references.bin           /var/lib/antifraud/references.bin
COPY --from=build /app/data/references.balltree.bin  /var/lib/antifraud/references.balltree.bin
COPY --from=build /app/data/references.kdtree.bin    /var/lib/antifraud/references.kdtree.bin

ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 80

ENTRYPOINT ["dotnet", "anti-fraud-api.dll"]
