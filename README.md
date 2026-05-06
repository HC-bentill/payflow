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
