# Observabilidade com OpenTelemetry + ClickHouse

**Starter kit de arquitetura** para coletar, armazenar e analisar traces, métricas e logs usando:

- **OpenTelemetry** (padrão aberto da CNCF)
- **OpenTelemetry Collector**
- **ClickHouse** (Cloud ou self-hosted)

A ideia é simples: qualquer aplicação instrumentada com OpenTelemetry pode enviar telemetria para este pipeline e consultar os dados com SQL no ClickHouse — sem vendor lock-in e com custo controlado.

---

## Para quem é

- Times que querem sair de APM caros (Datadog, New Relic, etc.) ou reduzir a conta
- Empresas que já usam (ou querem usar) ClickHouse e precisam de um caminho claro para observabilidade
- Engenheiros que precisam de um exemplo concreto e funcionando de OTEL → Collector → ClickHouse
- Quem busca uma base reutilizável para instrumentar APIs .NET (e, por extensão, outros runtimes)

---

## O que este repositório entrega

| Componente | Função |
|------------|--------|
| **Aplicação de exemplo** (ASP.NET Core 10) | Mostra como instrumentar uma API real com o SDK OpenTelemetry |
| **OpenTelemetry Collector** | Recebe OTLP, faz batch e exporta para ClickHouse |
| **Schema ClickHouse** | Tabelas otimizadas para traces/logs (codecs, ORDER BY, TTL, bloom filters) |
| **Queries prontas** | Latência (p50/p95/p99), error rate, throughput, spans lentos, trace por ID |
| **docker-compose** | Sobe o Collector apontando para ClickHouse Cloud (ou local) |

A aplicação de pedidos (`/api/orders`) é só o **exemplo de instrumentação**. O pipeline em si é genérico: qualquer serviço que fale OTLP pode usá-lo.

---

## Arquitetura

```
┌──────────────────────────────┐
│  Sua aplicação               │
│  (qualquer linguagem com     │
│   OpenTelemetry SDK)         │
│  Exemplo incluso: .NET 10    │
└──────────────┬───────────────┘
               │ OTLP (gRPC :4317 / HTTP :4318)
               ▼
┌──────────────────────────────┐
│  OpenTelemetry Collector     │
│  - receivers: otlp           │
│  - processors: batch,        │
│    memory_limiter, resource  │
│  - exporters: clickhouse     │
└──────────────┬───────────────┘
               │
               ▼
┌──────────────────────────────┐
│  ClickHouse                  │
│  (Cloud free tier ou self-   │
│   hosted)                    │
│  otel_traces / otel_logs /   │
│  otel_metrics_*              │
└──────────────────────────────┘
```

---

## Por que essa combinação

| Critério | OpenTelemetry + ClickHouse |
|----------|----------------------------|
| **Padrão aberto** | OTEL é o padrão de fato; evita lock-in de vendor |
| **Custo** | ClickHouse Cloud tem free tier generoso; self-hosted escala barato |
| **Performance** | ClickHouse responde agregações (p95, error rate) em milissegundos mesmo com volume alto |
| **Flexibilidade** | SQL puro — você controla o modelo de dados e as consultas |
| **Produtividade** | Collector já resolve batch, retry, memory limit e schema |

---

## Como usar no seu negócio

### 1. Suba o pipeline (uma vez)

```bash
# 1. Configure as credenciais do ClickHouse no docker-compose.yml
# 2. Crie o database (no SQL Console do ClickHouse):
#    CREATE DATABASE IF NOT EXISTS otel;

docker compose up -d
```

### 2. Instrumente sua aplicação

Qualquer app com OpenTelemetry SDK basta apontar o exporter OTLP para o Collector:

```text
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317
```

No exemplo deste repo (ASP.NET Core), a instrumentação já está pronta em `Program.cs` (ASP.NET Core + HttpClient + Runtime + spans manuais).

### 3. Consulte os dados

Use as queries em `clickhouse/queries.sql`:

- Latência p50 / p95 / p99 por endpoint
- Taxa de erro
- Throughput por minuto
- Spans mais lentos
- Trace completo por `TraceId`
- Child spans (operações internas)

---

## Exemplo incluso (ASP.NET Core 10)

Para não ficar só na teoria, o repositório traz uma API de pedidos instrumentada:

| Endpoint | O que demonstra |
|----------|-----------------|
| `POST /api/orders` | Spans manuais + child spans (validação, pagamento, persistência) |
| `GET /api/orders` | Listagem |
| `GET /api/orders?slow=true` | Latência artificial (útil para ver p95) |
| `GET /api/orders?fail=true` | Erro simulado (útil para error rate) |
| `GET /health` | Endpoint excluído do tracing |

Não é o produto final — é o **exemplo de como instrumentar**. Troque pelos seus serviços.

---

## Pré-requisitos

- .NET 10 SDK (para o exemplo)
- Docker + Docker Compose
- Conta no [ClickHouse Cloud](https://clickhouse.cloud) (free tier) **ou** ClickHouse self-hosted
- Visual Studio 2022/2026, Rider ou VS Code

---

## Subindo o exemplo completo

### 1. Credenciais ClickHouse Cloud

No console → seu serviço → **Connect** → aba **Username and password** (protocolo **Native**):

```yaml
# docker-compose.yml
environment:
  CLICKHOUSE_ENDPOINT: "clickhouse://SEU_HOST.clickhouse.cloud:9440?secure=true"
  CLICKHOUSE_DATABASE: "otel"
  CLICKHOUSE_USERNAME: "default"
  CLICKHOUSE_PASSWORD: "SUA_SENHA"
```

```sql
CREATE DATABASE IF NOT EXISTS otel;
```

### 2. Collector

```bash
docker compose up -d
docker compose logs -f otel-collector
# Esperado: "Everything is ready. Begin running and processing data."
```

### 3. API de exemplo

```bash
cd src/Portfolio.Api
dotnet run
# http://localhost:5080/swagger
```

### 4. Gerar tráfego e validar

```bash
# Postman, curl ou Swagger — várias chamadas em /api/orders
# Depois no ClickHouse:
SELECT count() FROM otel.otel_traces;
```

---

## Estrutura do repositório

```
├── src/Portfolio.Api/           # Exemplo de app instrumentada (.NET 10)
├── otel-collector/config.yaml   # Collector pronto para ClickHouse
├── clickhouse/
│   ├── schema.sql               # Tabelas otimizadas (opcional; exporter também cria)
│   └── queries.sql              # Consultas de análise prontas
├── docker-compose.yml
└── README.md
```

---

## Decisões técnicas (para entrevista / documentação interna)

- **OTLP como contrato**: qualquer linguagem/runtime com SDK OTEL funciona
- **Collector no meio**: batch, retry, memory limiter e desacoplamento app ↔ storage
- **ClickHouse com TTL (7 dias)**: controla storage no free tier; ajuste conforme retenção do negócio
- **Schema com codecs + bloom filters + ORDER BY (ServiceName, SpanName, Timestamp)**: otimizado para as queries mais comuns de observabilidade
- **Spans manuais + automáticos**: mostra o caminho completo de instrumentação

---

## Próximos passos possíveis

- Grafana + data source ClickHouse (dashboards)
- Sampling no Collector (controle de volume/custo)
- Dockerfile / deploy da API
- Mais instrumentações (EF Core, gRPC, mensageria)
- Multi-serviço (vários `ServiceName` no mesmo ClickHouse)

---

## Resumo

Este repositório não é “mais um demo de traces”. É uma **base reutilizável** para quem quer adotar OpenTelemetry com ClickHouse de forma prática:

1. Sobe o pipeline uma vez  
2. Instrumenta os serviços com OTEL (exemplo .NET incluso)  
3. Analisa tudo com SQL no ClickHouse  

Use como referência, adapte ao seu domínio e evolua conforme a necessidade do negócio.
