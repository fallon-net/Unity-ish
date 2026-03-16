# Client Architecture

## State Machine

States:

- `Disconnected`
- `Connecting`
- `ConnectedGood`
- `ConnectedWarn`
- `ConnectedBad`
- `Reconnecting`

Transitions are driven by transport callbacks and telemetry thresholds.

## Abstraction Boundaries

- `IAudioDeviceService`: enumerate/select input and output devices.
- `IPttInputService`: PTT key binding and key state.
- `IConnectionStateMachine`: central state transitions.

## Party-line Model

- Two logical channels: `PL-A` and `PL-B`.
- Independent listen/talk permissions.
- One active transmit at a time in MVP.
