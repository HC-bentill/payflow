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
