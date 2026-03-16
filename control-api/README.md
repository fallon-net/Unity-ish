# Control API (Go)

Reliability-first local control plane for Unity-ish intercom.

## Endpoints

- `GET /healthz` basic health probe
- `POST /v1/auth/login` credential check and short-lived app token
- `POST /v1/token/livekit` returns placeholder LiveKit token payload

## Run

1. Copy env file: `copy .env.example .env` (Windows) or `cp .env.example .env` (macOS)
2. Install deps: `go mod tidy`
3. Start: `go run ./cmd/server`
