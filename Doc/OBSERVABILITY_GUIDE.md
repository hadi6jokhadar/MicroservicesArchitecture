# Observability Guide — Distributed Tracing & Metrics

**Stack:** OpenTelemetry → Jaeger (traces) + Prometheus + Grafana (metrics)  
**Cost:** Fully free / open-source  
**Applies to:** All 6 .NET services + AI Python service

---

## How It Works

```
Your Services                    Collectors               UIs
─────────────────────────────    ───────────────          ─────────────────
IdentityService    ─┐            Jaeger :4317             Jaeger UI  :16686
TenantService      ─┤  OTLP gRPC ─────► (traces)         Grafana    :3100
NotificationService─┤                                     Prometheus :9090
FileManagerService ─┼  HTTP /metrics ──► Prometheus :9090
TranslationService ─┤  (scraped every 15s)
CategoryService    ─┤
AI Service (Python)─┘
```

- Every service sends **traces** via OTLP gRPC to Jaeger on port `4317`.
- Every service exposes a **`/metrics`** endpoint that Prometheus scrapes every 15 s.
- Grafana reads from both Jaeger (trace search) and Prometheus (dashboards/alerts).

### What is automatically instrumented

| Layer | .NET | Python (AI) |
|---|---|---|
| HTTP requests in | ✅ ASP.NET Core middleware | ✅ FastAPI middleware |
| HTTP requests out | ✅ HttpClient | ✅ HTTPX |
| Database queries | ✅ EF Core (with SQL text) | ✅ SQLAlchemy |
| Exceptions | ✅ Recorded on span | ✅ Recorded on span |
| Request duration histogram | ✅ Prometheus | ✅ Prometheus |
| Health/metrics endpoints | ⛔ Filtered out | ⛔ Filtered out |

### Configuration key

All services read `Observability:OtlpEndpoint` from `appsettings.json`:

```json
"Observability": {
  "OtlpEndpoint": "http://localhost:4317"
}
```

Change the endpoint value to point at a remote Jaeger collector in production — zero code changes required.

---

## Start the Observability Stack

From the repo root (`MicroservicesArchitecture/`):

```powershell
docker compose -f docker-compose.observability.yml up -d
```

Verify containers are running:

```powershell
docker compose -f docker-compose.observability.yml ps
```

| Service | URL |
|---|---|
| Jaeger UI | http://localhost:16686 |
| Prometheus | http://localhost:9090 |
| Grafana | http://localhost:3100 (admin / admin) |

Stop the stack:

```powershell
docker compose -f docker-compose.observability.yml down
```

---

## Install Python Packages (AI Service)

```powershell
cd src/Services/AI/AI.API
pip install -r requirements.txt
```

---

## Testing Traces in Jaeger

### 1. Start the observability stack and at least one service

```powershell
docker compose -f docker-compose.observability.yml up -d
# Then start IdentityService, TenantService, etc.
```

### 2. Make a real request

```powershell
# Example: login through the Identity service
curl -X POST http://localhost:5001/api/v1/auth/login `
  -H "Content-Type: application/json" `
  -H "x-tenant-id: your-tenant-id" `
  -d '{"email":"user@example.com","password":"pass"}'
```

### 3. View the trace in Jaeger

1. Open **http://localhost:16686**
2. In the **Service** dropdown, select `IdentityService`
3. Click **Find Traces**
4. Click any trace to see the full span tree with timing and SQL statements

### 4. Cross-service trace (Identity → Notification)

When Identity calls Notification via `INotificationServiceClient`, the trace context is propagated automatically via the `traceparent` header. Both spans appear in the same trace in Jaeger.

---

## Testing Metrics in Prometheus

### 1. Verify a service exposes /metrics

```powershell
curl http://localhost:5001/metrics
```

You should see output like:
```
# HELP http_server_active_requests Number of active HTTP server requests.
# TYPE http_server_active_requests gauge
http_server_active_requests{method="GET",route="..."} 0
...
```

### 2. Query in Prometheus UI

1. Open **http://localhost:9090**
2. In the Expression field, try:
   - `http_server_request_duration_seconds_bucket` — request latency histogram
   - `http_server_active_requests` — active requests gauge
   - `process_runtime_dotnet_gc_collections_count_total` — GC collections

### 3. Check scrape targets are UP

1. Go to **http://localhost:9090/targets**
2. All services listed in `prometheus.yml` should show **State: UP**

If a service shows DOWN, it means either the service is not running or the port is wrong.

---

## Setting Up Grafana

### Add Prometheus as a data source

1. Open **http://localhost:3100**, log in with `admin / admin`
2. Go to **Connections → Data sources → Add data source**
3. Choose **Prometheus**
4. Set URL: `http://prometheus:9090`
5. Click **Save & test**

### Add Jaeger as a data source

1. Go to **Connections → Data sources → Add data source**
2. Choose **Jaeger**
3. Set URL: `http://jaeger:16686`
4. Click **Save & test**

### Import a pre-built ASP.NET Core dashboard

1. Go to **Dashboards → Import**
2. Enter dashboard ID `10915`
3. Select your Prometheus data source
4. Click **Import**

This gives you: request rate, error rate, P50/P95/P99 latency, active requests — one panel per service.

---

## How the Code Works

### .NET Services

**`ObservabilityExtensions.cs`** (shared, written once in `IhsanDev.Shared.Infrastructure`):

```csharp
services.AddOpenTelemetry()
    .WithTracing(tracing => {
        tracing
            .SetResourceBuilder(...)          // tags spans with service name
            .AddAspNetCoreInstrumentation()   // instruments every HTTP request
            .AddHttpClientInstrumentation()   // instruments outgoing HTTP calls
            .AddEntityFrameworkCoreInstrumentation()  // instruments SQL queries
            .AddOtlpExporter(...);            // ships spans to Jaeger
    })
    .WithMetrics(metrics => {
        metrics
            .AddAspNetCoreInstrumentation()   // request counters, histograms
            .AddHttpClientInstrumentation()   // outgoing call counters
            .AddPrometheusExporter();         // enables /metrics endpoint
    });
```

Each service adds **2 lines** in `Program.cs`:

```csharp
builder.Services.AddPlatformObservability(builder.Configuration, "IdentityService");
// ... later ...
app.MapPrometheusScrapingEndpoint("/metrics");
```

### Python AI Service

`main.py` calls `_setup_tracing()` at startup, which:
1. Creates a `TracerProvider` with the service name
2. Adds a `BatchSpanProcessor` exporting via OTLP gRPC to Jaeger
3. Instruments HTTPX and SQLAlchemy automatically

`FastAPIInstrumentor.instrument_app(app)` wraps every route handler to create spans.

`Instrumentator().instrument(app).expose(app)` adds the `/metrics` endpoint for Prometheus.

All OTel imports are wrapped in `try/except ImportError` so the service starts normally even if the packages are not installed yet.

---

## Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| No traces in Jaeger | `OtlpEndpoint` wrong or Jaeger not running | Check `docker ps`, verify port 4317 is open |
| Prometheus target is DOWN | Service not running or wrong port | Check `prometheus.yml` matches service `Urls` in appsettings |
| No metrics at `/metrics` | `MapPrometheusScrapingEndpoint` missing | Verify it is called before `app.Run()` |
| Python service no traces | Packages not installed | Run `pip install -r requirements.txt` |
| Build error: package not found | OTel packages not in Directory.Packages.props | Verify versions are added to `Directory.Packages.props` |

---

## Production Notes

- Change `OtlpEndpoint` to your hosted Jaeger URL — **no code changes needed**
- Grafana and Prometheus should be deployed with persistent volumes in production
- Consider enabling Jaeger's remote storage backend (Elasticsearch/Cassandra) for retention beyond the in-memory default
