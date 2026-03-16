# Unity-ish Intercom

Self-hosted, local-first intercom scaffold for live event production with two party-lines and reliability-first design.

## Architecture

- `unity-client`: Cross-platform Unity app shell (macOS + Windows) with audio and input abstractions.
- `control-api`: Go service for auth, token issuance, health checks, and config state.
- `infra`: Docker Compose stack for LiveKit SFU + Redis.
- `admin-web`: Lightweight operator/admin status UI.

Text diagram:

`Unity Client (Mac/Win) -> Control API (Go) -> LiveKit Token`
`Unity Client (Mac/Win) <-> LiveKit SFU (WebRTC/Opus)`
`Admin Web -> Control API`
`Control API -> SQLite`

## Prerequisites

- Docker Desktop (Windows or macOS)
- Go 1.22+
- Node.js 20+
- Unity Hub + Unity 2022 LTS or newer

## Quickstart

1. Start media infrastructure:
   - `cd infra`
   - `copy .env.example .env` (Windows) or `cp .env.example .env` (macOS)
   - `docker compose up -d`
2. Start control API:
   - `cd ../control-api`
   - `copy .env.example .env` (Windows) or `cp .env.example .env` (macOS)
   - `go run ./cmd/server`
3. Start admin web:
   - `cd ../admin-web`
   - `npm install`
   - `npm run dev`
4. Open Unity project folder `unity-client` in Unity Hub and wire in a LiveKit Unity SDK package.

## Reliability Defaults

- Wired LAN first.
- Opus mono at 48 kHz.
- Auto-reconnect with capped backoff.
- Explicit health states: Good, Warn, Bad, Reconnecting.
- Local service IP fallback (no cloud dependency).

## Milestone Roadmap (2 Party-lines)

1. M1: Login, token issue, connect to LiveKit.
2. M2: Party-lines `PL-A` and `PL-B`, listen toggles.
3. M3: Hold-to-talk (PTT) per line and force mute.
4. M4: Device select persistence and reconnect recovery.
5. M5: Production hardening (packet loss, cable pull, server restart drills).
