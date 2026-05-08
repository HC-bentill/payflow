# Required GitHub Actions Secrets

Configure these in GitHub -> Settings -> Secrets and variables -> Actions:

## CI/CD Secrets (required for pipeline)
None required for Phase 7 — pipeline uses ephemeral test credentials inline.

## Future secrets (Phase 9 — homelab deployment)
REGISTRY_HOST         — local registry hostname (e.g. registry.homelab.local:5000)
REGISTRY_USERNAME     — registry auth username
REGISTRY_PASSWORD     — registry auth password
HOMELAB_SSH_HOST      — homelab server IP or hostname
HOMELAB_SSH_USER      — SSH username
HOMELAB_SSH_KEY       — private SSH key for deployment

## Application secrets (never commit these)
JWT_KEY               — production JWT signing key (min 32 chars)
POSTGRES_PASSWORD     — production database password
REDIS_PASSWORD        — production Redis password (if auth enabled)
WEBHOOK_SIGNING_KEY   — base secret for webhook HMAC signatures
