# Contexto = raiz do repositório (este ficheiro ao lado de src/, resources/, …).
#
#   docker build -t rinha-ddd-dotnet .
#
# Executa na raiz do clone, não dentro de src/.
# =============================================================================
# STAGE 1: Build + publish + prebuild (references.bin + caches de índice)
# =============================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/anti-fraud-api/anti-fraud-api.csproj src/anti-fraud-api/
COPY src/anti-fraud-core/anti-fraud-core.csproj src/anti-fraud-core/
COPY src/anti-fraud-application/anti-fraud-application.csproj src/anti-fraud-application/
COPY src/anti-fraud-infrastructure/anti-fraud-infrastructure.csproj src/anti-fraud-infrastructure/
RUN dotnet restore src/anti-fraud-api/anti-fraud-api.csproj

COPY . .
WORKDIR /src/src/anti-fraud-api
RUN dotnet publish anti-fraud-api.csproj -c Release -o /app/publish /p:UseAppHost=false

RUN mkdir -p /app/data \
 && dotnet /app/publish/anti-fraud-api.dll --prebuild \
        /src/resources/references.json.gz \
        /app/data/references.bin \
        /app/data/references.balltree.bin \
        /app/data/references.kdtree.bin \
 && rm -f /app/publish/shared/resources/references.json.gz \
 && ls -lh /app/data

# =============================================================================
# STAGE 2: Runtime
# =============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

RUN mkdir -p /var/lib/antifraud
COPY --from=build /app/data/references.bin           /var/lib/antifraud/references.bin
COPY --from=build /app/data/references.balltree.bin  /var/lib/antifraud/references.balltree.bin
COPY --from=build /app/data/references.kdtree.bin    /var/lib/antifraud/references.kdtree.bin

ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 80

ENTRYPOINT ["dotnet", "anti-fraud-api.dll"]
