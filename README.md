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
