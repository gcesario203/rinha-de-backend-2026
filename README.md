# Rinha de Backend 2026 — Detecção de fraudes (busca vetorial)

![cover](/misc/cover.png)

[Português — desafio](#português) · [English — challenge](#english) · [**Solução implementada**](#solução-implementada)

### Prévia do ranking oficial @ [rinhadebackend.com.br](https://rinhadebackend.com.br/)

---

## Solução implementada

Esta pasta contém uma API em **.NET 10 (C#)** que classifica transações com **k-NN** (k = 5) sobre um conjunto de referência de **3 milhões** de vetores de **14 dimensões**, sob os limites do desafio (**1 vCPU total**, **350 MB RAM** entre HAProxy + duas réplicas da API).

**Participação:** submissão registada em [`participants/gcesario203.json`](./participants/gcesario203.json) — identificador **`gcesario203-dotnet-ddd-kd-tree`**, repositório [**github.com/gcesario203/rinha-de-backend-2026**](https://github.com/gcesario203/rinha-de-backend-2026).

### Arquitetura em alto nível

| Componente | Papel |
|------------|--------|
| **HAProxy** (`src/haproxy/haproxy.cfg`) | Entrada na porta **9999**; `balance roundrobin` (exigência do desafio — ver [docs/br/ARQUITETURA.md](./docs/br/ARQUITETURA.md)); health check `GET /ready`; timeouts e `option abortonclose` / `redispatch` para reduzir filas sob saturação. |
| **Duas APIs ASP.NET Core** | Mesma imagem Docker; cada uma com **0,475 CPU** e **167 MB** RAM (ver `src/docker-compose.yml`). |
| **Dataset** | `references.bin` em **memory-mapped file** (~168 MB de vetores + labels); leitura sem carregar tudo para o heap gerenciado. |
| **Índice** | Estratégia configurável (`AntiFraud:ClassifierStrategy`): **ball tree** (`references.balltree.bin`, DFS closer-first + poda por bola) ou **KD-tree** (`references.kdtree.bin`, poda por hiperplano). Alternativa **força bruta** para diagnóstico. Todas usam a mesma interface de domínio (`INeighborhoodClassifier`). |
| **Regra de decisão** | Voto dos 5 vizinhos mais próximos (distância euclidiana); limiar **0,6** para marcar fraude (ver motor de inferência). |

### Motivações das escolhas principais

1. **Índice espacial em vez de força bruta**  
   Varredura linear em 3M × 14 floats por pedido não cabe no orçamento de latência. A **ball tree** reduz visitas com bounds em bola; a **KD-tree** particiona por mediana na dimensão de maior amplitude e poda pelo plano de corte. Em 14D a poda não é “mágica”, mas ambas ganham face ao scan completo.

2. **Distância ao quadrado + SIMD (`VectorMath14`)**  
   Comparar por `dist²` evita `sqrt` desnecessários na poda; onde precisa de raio em espaço de poda, usa-se `WorstDist` já cacheado na fila de k vizinhos. SIMD alinha com o custo dominante (float math).

3. **Menos alocação no hot path**  
   Vetor da transação em `stackalloc`, `ReadOnlySpan<float>`, `string[]` para `known_merchants` com comparação ordinal, JSON com **source generation** (`AntiFraudJsonSerializerContext` no core), endpoints com `TypedResults` — tudo para não competir com o GC sob RAM apertada.

4. **Pré-build no Docker (`--prebuild`)**  
   Construir `references.bin` e os caches de índice (**`references.balltree.bin`** e **`references.kdtree.bin`**) no **build da imagem** evita minutos de CPU/I-O no arranque do contentor, cumpre health checks curtos e remove dependência de volume partilhado lento entre réplicas.

5. **Cache em disco (formato próprio)**  
   Ball tree: serialização pré-order com **centróide e raio também nas folhas** (necessário para a poda equivaler à árvore em memória). KD-tree: pré-order com folhas (índices) ou nós internos (eixo + valor de corte).

6. **Warmup antes de `MarkAsReady`**  
   `PreFault` no mmap + centenas de queries sintéticas aquecem páginas e o JIT (TieredPGO + ReadyToRun) antes do tráfego real, reduzindo a cauda do **p99** causada por cold start + fila.

7. **Kestrel e GC afinados**  
   `ServerGC`, limites de body, headers e keep-alive enxutos; `ThreadPool.SetMinThreads` para amortecer ramp-up de concorrência.

### O que foi experimentado e descartado (e porquê)

| Ideia | Resultado |
|-------|-----------|
| **COBT** (reordenar o bin por DFS) | Ganho de localidade insuficiente frente ao custo de reescrever 3M linhas; I/O no build/volume foi pior do que o benefício. |
| **IVF-PQ** | Perda de recall em 14D + custo de cold start; pontuação pior que a ball tree exata. |
| **Best-first global com heap** | Mais trabalho por pedido (heap + menos poda cedo) do que DFS closer-first neste regime. |
| **Folhas maiores (ex.: 90)** | Mais comparações por folha; warm-up medido piorou vs folha 60. |
| **Native AOT** | Sem JIT/PGO, o núcleo numérico (k-NN) ficou mais lento; **p99** e `http_errors` pioraram vs JIT + TieredPGO + R2R. Mantido **JIT** para este perfil. |

### Estrutura de pastas (código)

- `src/anti-fraud-api` — host ASP.NET Core, endpoints, `Program.cs`, `--prebuild`, warmup, materialização.
- `src/anti-fraud-core` — ball tree, KD-tree, dataset mmap, serialização JSON partilhada, motor k-NN / filas.
- `src/anti-fraud-application` — serviços de aplicação (classificador, transação, fraud engine).
- `src/anti-fraud-infrastructure` — recursos JSON (heurísticas, MCC), DI.
- `src/docker-compose.yml` — orquestração local com limites alinhados ao `config.json`.
- `Dockerfile` (na raiz) — imagem da API; contexto de build = raiz do repo.
- `src/haproxy/haproxy.cfg` — configuração do balanceador.

### Como correr localmente

Na raiz do repositório (contexto de build `..` relativamente a `src/docker-compose.yml`):

```bash
docker compose -f src/docker-compose.yml up --build
```

**Build da imagem:** o `Dockerfile` está na **raiz do repositório**. Na raiz do clone:

```bash
docker build -t rinha-ddd-dotnet .
```

Em pipelines ou UI, o contexto tem de ser a **raiz do projeto** (pastas `src/` e `resources/` visíveis); usar só `src/` como contexto faz falhar os `COPY`.

Health: `http://localhost:9999/ready`  
Inferência: `POST http://localhost:9999/fraud-score` (corpo conforme [docs/br/API.md](./docs/br/API.md)).

Testes de carga (perfil `bench`):

```bash
docker compose -f src/docker-compose.yml --profile bench run --rm k6-smoke
docker compose -f src/docker-compose.yml --profile bench run --rm k6-test
```

---

## Português

A **Rinha de Backend** é uma competição amistosa em que se constrói um backend sob restrições de CPU, memória e arquitetura. Esta edição é **detecção de fraudes com busca vetorial**.

**Documentação oficial do desafio:** [**docs/br/README.md**](./docs/br/README.md)

### Edições anteriores

- [**2025** — Payment Processor](https://github.com/zanfranceschi/rinha-de-backend-2025)
- [**2024** — Crébitos](https://github.com/zanfranceschi/rinha-de-backend-2024-q1)
- [**2023** — CRUD de Pessoas](https://github.com/zanfranceschi/rinha-de-backend-2023-q3)

### Redes sociais

- [Website](https://rinhadebackend.com.br/) · [Discord](https://discord.gg/Eca6gJba8R) · [X](https://x.com/rinhadebackend) · [LinkedIn](https://www.linkedin.com/company/108194083) · [Bluesky](https://bsky.app/profile/rinhadebackend.bsky.social)

---

## English

**Rinha de Backend** is a friendly competition under CPU, memory, and architecture constraints. This edition is **fraud detection with vector search**.

**Official challenge docs:** [**docs/en/README.md**](./docs/en/README.md)

### Implementation summary

**.NET 10** minimal APIs, **k-NN (k = 5)** over a **memory-mapped** reference set (~3M × 14 floats), configurable **ball tree** or **KD-tree** index baked at **Docker build** time (`references.balltree.bin`, `references.kdtree.bin`), **HAProxy** fronting two replicas, **source-generated JSON**, **SIMD** distance helpers, **warmup** before readiness, **JIT + TieredPGO + ReadyToRun** (Native AOT was tried and reverted for this numeric-heavy workload). See the Portuguese section above for the full rationale table.

### Previous editions & social

Same links as in the Portuguese section: [2025](https://github.com/zanfranceschi/rinha-de-backend-2025), [2024](https://github.com/zanfranceschi/rinha-de-backend-2024-q1), [2023](https://github.com/zanfranceschi/rinha-de-backend-2023-q3) · [Website](https://rinhadebackend.com.br/) · [Discord](https://discord.gg/Eca6gJba8R)
