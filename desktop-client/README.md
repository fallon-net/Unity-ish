# Desktop Client

Electron + React desktop operator client for Unity-ish.

## Why this client exists

The product is named Unity-ish, but the voice intercom stack does not require the Unity game engine. This desktop client is the primary Mac-first and Windows-portable runtime.

## Features

- Login against the local Go control API
- Fetch signed LiveKit room tokens for `PL-A` and `PL-B`
- Join both party-lines simultaneously
- Hold-to-talk using mouse or `F1` / `F2`
- Per-device input and output selection
- Health badge driven by LiveKit connection quality

## Run

1. `npm install`
2. `npm run dev`

## Build

1. `npm run build`
2. `npm run start`

## Notes

- The renderer uses `livekit-client` directly.
- Docker is only required for infrastructure in `infra`.
- The legacy `unity-client` folder is retained temporarily for reference but is no longer the primary client.
