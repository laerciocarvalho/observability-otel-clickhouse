-- Queries ajustadas ao schema real do ClickHouse Cloud
-- (SpanKind = 'Server' | 'Internal', StatusCode = 'Ok' | 'Unset' | 'Error')

-- ============================================================
-- 1. Visão geral dos dados
-- ============================================================
SELECT
    ServiceName,
    SpanKind,
    StatusCode,
    count() AS qtd
FROM otel.otel_traces
GROUP BY ServiceName, SpanKind, StatusCode
ORDER BY qtd DESC;


-- ============================================================
-- 2. Latência por endpoint (spans de servidor)
-- ============================================================
SELECT
    SpanName,
    count() AS requests,
    round(quantile(0.50)(Duration) / 1e6, 2) AS p50_ms,
    round(quantile(0.95)(Duration) / 1e6, 2) AS p95_ms,
    round(quantile(0.99)(Duration) / 1e6, 2) AS p99_ms,
    round(avg(Duration) / 1e6, 2) AS avg_ms
FROM otel.otel_traces
WHERE ServiceName = 'portfolio-api'
  AND SpanKind = 'Server'
GROUP BY SpanName
ORDER BY requests DESC;


-- ============================================================
-- 3. Taxa de erro por endpoint
-- ============================================================
SELECT
    SpanName,
    count() AS total,
    countIf(StatusCode = 'Error') AS errors,
    round(if(total = 0, 0, errors / total * 100), 2) AS error_rate_pct
FROM otel.otel_traces
WHERE ServiceName = 'portfolio-api'
  AND SpanKind = 'Server'
GROUP BY SpanName
ORDER BY error_rate_pct DESC;


-- ============================================================
-- 4. Throughput (requests por minuto)
-- ============================================================
SELECT
    toStartOfMinute(Timestamp) AS minute,
    count() AS requests
FROM otel.otel_traces
WHERE ServiceName = 'portfolio-api'
  AND SpanKind = 'Server'
  AND Timestamp >= now() - INTERVAL 30 MINUTE
GROUP BY minute
ORDER BY minute;


-- ============================================================
-- 5. Spans mais lentos (top 20)
-- ============================================================
SELECT
    Timestamp,
    SpanName,
    SpanKind,
    round(Duration / 1e6, 2) AS duration_ms,
    StatusCode,
    TraceId
FROM otel.otel_traces
WHERE ServiceName = 'portfolio-api'
ORDER BY Duration DESC
LIMIT 20;


-- ============================================================
-- 6. Trace completo por TraceId
-- (substitua o valor pelo TraceId que quiser inspecionar)
-- ============================================================
SELECT
    Timestamp,
    SpanId,
    ParentSpanId,
    SpanName,
    SpanKind,
    round(Duration / 1e6, 2) AS duration_ms,
    StatusCode,
    StatusMessage
FROM otel.otel_traces
WHERE TraceId = 'COLE_O_TRACE_ID_AQUI'
ORDER BY Timestamp;


-- ============================================================
-- 7. Child spans (Internal) mais comuns
-- ============================================================
SELECT
    SpanName,
    count() AS qtd,
    round(quantile(0.95)(Duration) / 1e6, 2) AS p95_ms
FROM otel.otel_traces
WHERE ServiceName = 'portfolio-api'
  AND SpanKind = 'Internal'
GROUP BY SpanName
ORDER BY qtd DESC;


-- ============================================================
-- 8. Logs de erro correlacionados (se houver logs exportados)
-- ============================================================
SELECT
    Timestamp,
    SeverityText,
    Body,
    TraceId,
    SpanId
FROM otel.otel_logs
WHERE ServiceName = 'portfolio-api'
  AND SeverityText IN ('Error', 'Fatal', 'ERROR', 'FATAL')
  AND Timestamp >= now() - INTERVAL 1 HOUR
ORDER BY Timestamp DESC
LIMIT 50;


-- ============================================================
-- 9. Resumo do serviço (últimas 24h)
-- ============================================================
SELECT
    ServiceName,
    count() AS total_spans,
    countIf(StatusCode = 'Error') AS error_spans,
    round(quantile(0.95)(Duration) / 1e6, 2) AS p95_ms
FROM otel.otel_traces
WHERE Timestamp >= now() - INTERVAL 24 HOUR
GROUP BY ServiceName
ORDER BY total_spans DESC;
