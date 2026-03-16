# Infra

Local media infrastructure for Unity-ish intercom.

## Services

- LiveKit SFU
- Redis

## Start

1. `copy .env.example .env` (Windows) or `cp .env.example .env` (macOS)
2. `docker compose up -d`
3. Verify with `docker ps`

## Stop

- `docker compose down`

## Notes

- For production, pin a specific LiveKit image tag.
- Keep host on wired LAN with UPS power.
- Adjust UDP range to match venue firewall policy.
