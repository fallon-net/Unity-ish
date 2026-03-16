# Unity Client

Unity app shell for Unity-ish intercom with macOS and Windows support from day 1.

## Goals

- Two party-lines (`PL-A`, `PL-B`)
- Hold-to-talk per line
- Listen toggles per line
- Device selection and persistence
- Reconnect and health state indicators

## Unity Version

Use Unity 2022 LTS or newer.

## Setup

1. Open this folder as a Unity project in Unity Hub.
2. Add a WebRTC/LiveKit Unity package.
3. Wire transport calls into script stubs under `Assets/Scripts`.
4. Set `config/client.sample.json` values in your runtime config path.

## Cross-platform Notes

- Keep platform-specific logic in adapters only.
- Use shared interfaces for audio devices and PTT input.
- Start with in-window hotkeys for reliability and permissions simplicity.
