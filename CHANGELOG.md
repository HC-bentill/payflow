# Changelog

All notable changes to PayFlow are documented here.
Format based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

## [Unreleased]

## [0.7.0] — Phase 7: CI/CD
### Added
- GitHub Actions pipeline: lint, build, security scan, test, Docker build
- Code coverage enforcement (minimum 70%)
- Vulnerable package scanning on every push
- Docker image tagging with git SHA
- Branch protection documentation

## [0.6.0] — Phase 6: Observability
### Added
- Prometheus metrics: payments, webhooks, rate limits, Kafka lag
- Grafana dashboard with 10 panels (pre-provisioned)
- Serilog structured JSON logging
- Correlation ID middleware — X-Correlation-ID on every request
- Kafka consumer lag monitoring (30s interval)

## [0.5.0] — Phase 5: Rate Limiting
### Added
- Redis sliding window rate limiter
- Per-tenant tier limits: Free (100/min), Pro (1000/min)
- Auth endpoint IP-based limits (brute force protection)
- X-RateLimit-Limit, X-RateLimit-Remaining, X-RateLimit-Reset headers
- 429 responses with Retry-After header

## [0.4.0] — Phase 4: Event-Driven Workers
### Added
- Kafka consumer: WebhookDispatchConsumer (payment.events -> webhook.delivery)
- Kafka consumer: WebhookDeliveryWorker (exponential backoff, 5 attempts)
- Dead letter queue: webhook.dlq for permanently failed deliveries
- Kafka consumer: NotificationConsumer (stubbed)
- Kafka consumer: DLQMonitorConsumer
- HMAC-SHA256 webhook signatures (X-PayFlow-Signature header)
- Webhook delivery logs with full attempt history
- Webhook replay API

## [0.3.0] — Phase 3: Core Payments
### Added
- POST /v1/payments — idempotent payment creation
- Redis distributed lock for concurrent duplicate prevention
- Double-entry ledger (Debit + Credit per payment)
- Wallet balance computed from ledger aggregates
- Kafka event publishing: PaymentProcessed -> payment.events
- Tenant-scoped payment listing with pagination

## [0.2.0] — Phase 2: Authentication
### Added
- Tenant registration with one-time API key issuance (pf_live_ prefix)
- SHA256 API key hashing — raw key never stored
- JWT issuance endpoint (15 min expiry, ClockSkew = Zero)
- TenantContextMiddleware — tenant isolation on every request
- Role-based access: Admin, Developer, ReadOnly

## [0.1.0] — Phase 1: Foundation
### Added
- Solution structure: Api, Domain, Application, Infrastructure, Tests
- EF Core with PostgreSQL — tenants, payments, ledger, wallets, webhooks
- Redis and Kafka wired up
- GET /health — Postgres + Redis health checks
- GET /metrics — Prometheus scrape endpoint
- Docker Compose for local development
- Multi-stage Dockerfile
