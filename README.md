# PayFlow

PayFlow is a production-grade, multi-tenant payment processing API scaffolded for Phase 1 with ASP.NET Core 10, PostgreSQL, Redis, and Kafka. This phase establishes the solution boundaries, persistence model, health and metrics surface, local development infrastructure, and CI build/test workflow without adding Kubernetes or registry publishing concerns.

```text
+--------+      +-------------+      +-----------------+      +----------+
| Client | ---> | API Gateway | ---> | Payment Service | ---> | Postgres |
+--------+      +-------------+      +-----------------+      +----------+
                                             |
                                             +---------------> +-------+
                                             |                 | Redis |
                                             |                 +-------+
                                             |
                                             +---------------> +-------+
                                                               | Kafka |
                                                               +-------+
```

## Roadmap
`Phase 1`   Foundation
          Solution structure, EF Core + PostgreSQL, Redis + Kafka wiring,
          GET /health, GET /metrics, Docker Compose, multi-stage Dockerfile

`Phase 2`   Multi-Tenant Authentication
          Tenant registration, one-time API key issuance (pf_live_ prefix),
          SHA256 key hashing, JWT issuance (15 min), TenantContextMiddleware,
          role-based access (Admin, Developer, ReadOnly)

`Phase 3`   Idempotent Payment Processing
          POST /v1/payments with Idempotency-Key header, Redis distributed lock,
          double-entry ledger (Debit + Credit), wallet balance computed from ledger,
          Kafka event publishing (PaymentProcessed → payment.events),
          paginated payment listing, tenant isolation

`Phase 4`   Event-Driven Workers + Webhooks
          WebhookDispatchConsumer (payment.events → webhook.delivery),
          WebhookDeliveryWorker with exponential backoff (3s→10s→60s→600s),
          dead letter queue (webhook.dlq), DLQMonitorConsumer,
          HMAC-SHA256 webhook signatures (X-PayFlow-Signature),
          webhook delivery logs, replay API, NotificationConsumer (stubbed)

`Phase 5`   Rate Limiting
          Redis sliding window rate limiter, per-tenant tier limits
          (Free: 100/min, Pro: 1000/min), IP-based auth endpoint limits,
          X-RateLimit-Limit / Remaining / Reset headers, 429 + Retry-After

`Phase 6`   Observability
          Custom Prometheus metrics (payments, webhooks, rate limits, Kafka lag),
          Grafana dashboard (10 panels, pre-provisioned), Serilog structured
          JSON logging, correlation ID middleware (X-Correlation-ID),
          Kafka consumer lag monitor

`Phase 7`   CI/CD
          GitHub Actions pipeline (lint → build → security scan → test → Docker build),
          NuGet vulnerability scanning, code coverage enforcement (min 70%),
          Docker image tagged with git SHA, branch protection, .editorconfig,
          CHANGELOG.md

──────────────────────────────────────────────────────────────────

`Phase 7.5`  Ledger Rework — Sender / Receiver Wallets
           Multi-party payment model, SenderWalletId + ReceiverWalletId,
           separate ledger entries per wallet, real balance movement

`Phase 8`   Homelab Setup
           Ubuntu VMs on VirtualBox, k3s cluster (1 control plane + 2 workers),
           Postgres + Redis + Grafana on dedicated VM, cluster networking,
           local Docker registry

`Phase 9`   Production Deployment
           Kubernetes manifests (Deployment, Service, ConfigMap, Secret),
           Kafka via Bitnami Helm chart, GitHub Actions push to local registry,
           kubectl rollout, production Grafana dashboard connected to live cluster

## Prerequisites

- Docker
- .NET 10 SDK
- dotnet-ef tool

## Local Dev Quickstart

1. `docker-compose up -d`
2. `dotnet tool install -g dotnet-ef` (if not installed)
3. `dotnet ef database update --project src/PayFlow.Infrastructure --startup-project src/PayFlow.Api`
4. `dotnet run --project src/PayFlow.Api`
5. Open `https://localhost:5001/swagger`

PostgreSQL is exposed from Docker Compose on `localhost:5433` to avoid collisions with native PostgreSQL installations that commonly use `5432`.

## Authentication

PayFlow uses a two-step auth flow:

### 1. Register a tenant

POST `/v1/auth/register`

```json
{
  "name": "my-store",
  "tier": "Free"
}
```

Returns a one-time API key: `"pf_live_..."`.
Store it securely - it cannot be retrieved again.

### 2. Exchange for a JWT

POST `/v1/auth/token`

```json
{
  "apiKey": "pf_live_...",
  "role": "Developer"
}
```

Returns a JWT valid for 15 minutes.

### 3. Use the JWT

Add to every request header:

```text
Authorization: Bearer eyJ...
```

JWTs expire after 15 minutes. Re-issue via `/v1/auth/token` using your API key.

## Payments API

All endpoints require:

```text
Authorization: Bearer <jwt>
```

### Create a payment

POST `/v1/payments`

Headers:

```text
Idempotency-Key: <merchant-generated-key>
Authorization: Bearer <jwt>
```

Body:

```json
{
  "amount": 100.00,
  "currency": "USD",
  "description": "Order #1234",
  "metadata": "{\"orderId\": \"1234\"}"
}
```

IMPORTANT: Generate the idempotency key BEFORE entering any retry loop.
The same key replayed returns the same response without reprocessing.

### Get a payment

GET `/v1/payments/{id}`

### List payments

GET `/v1/payments?page=1&pageSize=20`

### Get wallet balance

GET `/v1/payments/wallet/{currency}`

## Webhooks

PayFlow delivers real-time event notifications to your server via webhooks.

### Register a webhook endpoint 
This is to say call this endpoint each time a transaction is attempted/made

POST `/v1/webhooks/endpoints`

```json
{
  "url": "https://your-server.com/webhooks/payflow",
  "secret": "your-webhook-secret-min-16-chars"
}
```

### Verify webhook signatures

Every webhook request includes the header:

```text
X-PayFlow-Signature: sha256=<signature>
```

Verify on your server:

```text
expected = "sha256=" + HMAC-SHA256(requestBody, yourSecret)
if (expected != X-PayFlow-Signature) reject the request
```

### Replay a failed delivery

POST `/v1/webhooks/deliveries/{deliveryLogId}/replay`

### Retry schedule

Attempt 1: immediate
Attempt 2: 3 seconds
Attempt 3: 10 seconds
Attempt 4: 60 seconds
Attempt 5: 600 seconds
After 5 failures: permanently failed (visible in delivery logs)

## Rate Limiting

PayFlow enforces per-tenant rate limits using a Redis sliding window algorithm.

### Limits by tier

| Tier | Limit | Window |
|------|-------|--------|
| Free | 100 req | 60s |
| Pro | 1,000 req | 60s |

### Auth endpoint limits (by IP)

| Endpoint | Limit | Window |
|----------|-------|--------|
| POST `/v1/auth/register` | 10 req | 60s |
| POST `/v1/auth/token` | 20 req | 60s |

### Response headers

Every API response includes:

```text
X-RateLimit-Limit:     Your tier's request limit
X-RateLimit-Remaining: Requests remaining in current window
X-RateLimit-Reset:     Unix timestamp when window resets
```

### On limit exceeded (429)

```text
Retry-After: <seconds>
Body: { "error": "rate_limit_exceeded", "retryAfter": <seconds>, ... }
```

### Why Redis sliding window?

- Survives app restarts - state lives in Redis, not in-process memory.
- Works across multiple replicas - all instances share the same Redis counter.
- Fairer than fixed window - prevents burst abuse at window boundaries.

## Observability

### Metrics - Prometheus

Metrics are exposed at GET `/metrics` in Prometheus text format.

Key metrics:

```text
payflow_payments_total{status,currency,tier}     - payment outcomes
payflow_payment_amount{currency,tier}            - payment amount distribution
payflow_webhook_deliveries_total{status}         - webhook delivery outcomes
payflow_rate_limit_hits_total{tier,endpoint}     - rate limit breaches
payflow_kafka_consumer_lag{topic,consumer_group} - Kafka processing backlog
```

### Grafana Dashboard

1. `docker-compose up -d`
2. Open `http://localhost:3000` (admin / admin)
3. Dashboard `PayFlow — Production Dashboard` is pre-loaded automatically.

Prometheus runs at `http://localhost:9090` and scrapes the API on `host.docker.internal:5000`.

### Correlation IDs

Every request gets a correlation ID propagated through all log lines.
Pass your own with `X-Correlation-ID: my-trace-id`, or let PayFlow generate one and return it in the response header.

### Structured Logs

All logs are JSON formatted for stdout, so Docker and future orchestrators can parse them directly.

Example Compact JSON log line:

```json
{
  "@t": "2025-01-01T00:00:00.0000000Z",
  "@mt": "Payment {PaymentId} succeeded for tenant {TenantId}",
  "PaymentId": "abc-123",
  "TenantId": "xyz-456",
  "CorrelationId": "a1b2c3d4",
  "EnvironmentName": "Production"
}
```

## Solution Projects

- `src/PayFlow.Api` - ASP.NET Core 10 Web API host, controllers, Swagger, JWT auth, health, and Prometheus metrics.
- `src/PayFlow.Domain` - Domain entities, enums, and repository interfaces with no infrastructure dependencies.
- `src/PayFlow.Application` - Application services, MediatR registration, common result type, and CQRS-style health query.
- `src/PayFlow.Infrastructure` - EF Core PostgreSQL DbContext, entity configurations, repositories, Redis health probe, and infrastructure registration.
- `tests/PayFlow.Api.Tests` - xUnit integration/smoke tests for the API host.

## Running Tests

```bash
dotnet test
```

Kubernetes deployment guide will be added in a future phase.

## CI/CD

### Pipeline
Every push to main or develop triggers the full pipeline:
  Lint -> Build -> Security Scan -> Test + Coverage -> Docker Build

### Branch strategy
  main     — production-ready only, protected, requires PR + passing CI
  develop  — integration branch
  feature/* — feature branches, merge to develop via PR

### Coverage threshold
Minimum 70% line coverage enforced. Pipeline fails below this.

### Security scanning
Every push scans all NuGet packages (including transitive) for known CVEs.
Pipeline fails if any vulnerable package is detected.

### Docker image tags
  payflow-api:sha-   — immutable, tied to exact commit
  payflow-api:main              — latest main branch build
  payflow-api:latest            — latest production build

### Secrets
See .github/SECRETS.md for required secrets.
Never commit secrets — use GitHub Actions Secrets only.

## Coding standards — enforce throughout
- Workflow YAML indented with 2 spaces (GitHub Actions standard)
- All secrets referenced via ${{ secrets.NAME }} — never hardcoded
- All jobs explicitly declare needs: to enforce ordering
- Cache keys include file hashes so cache invalidates on dependency changes
- Test environment variables use __ notation (ConnectionStrings__Postgres)
  for ASP.NET Core environment variable configuration override
- dotnet build must use /WarnAsError — zero warning tolerance in CI

## Definition of done
- Push to main -> GitHub Actions pipeline triggers automatically
- Lint job fails if dotnet format detects violations
- Build job fails if any compiler warning exists
- Security job fails if any vulnerable NuGet package detected
- Test job: all tests pass, coverage report generated, posted to job summary
- Coverage below 70% -> pipeline fails
- Docker job: image builds successfully, tagged with git SHA
- Summary job: pipeline result table posted to GitHub Actions summary
- .editorconfig enforces consistent formatting across team
- CHANGELOG.md accurately reflects all 7 phases
