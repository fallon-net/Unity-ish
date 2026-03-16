import { useEffect, useRef, useState } from "react";
import { IntercomClient, loadClientConfig, loadPreferences, savePreferences, type ChannelId, type HealthSnapshot } from "./lib/intercom";

const channels: ChannelId[] = ["PL-A", "PL-B"];

type DeviceState = {
    inputId: string;
    outputId: string;
    inputDevices: MediaDeviceInfo[];
    outputDevices: MediaDeviceInfo[];
};

export function App() {
    const [username, setUsername] = useState("ops");
    const [password, setPassword] = useState("changeme");
    const [error, setError] = useState("");
    const [connected, setConnected] = useState(false);
    const [busy, setBusy] = useState(false);
    const [health, setHealth] = useState<HealthSnapshot>({ status: "disconnected", maxRttMs: -1, maxLossPercent: -1 });
    const [listenState, setListenState] = useState<Record<ChannelId, boolean>>({ "PL-A": true, "PL-B": true });
    const [talkState, setTalkState] = useState<Record<ChannelId, boolean>>({ "PL-A": false, "PL-B": false });
    const [devices, setDevices] = useState<DeviceState>({ inputId: "", outputId: "", inputDevices: [], outputDevices: [] });
    const clientRef = useRef<IntercomClient | null>(null);

    useEffect(() => {
        let cancelled = false;

        async function bootstrap() {
            const config = await loadClientConfig();
            if (cancelled) {
                return;
            }

            const client = new IntercomClient(config, setHealth);
            clientRef.current = client;

            const inputDevices = await client.getInputDevices();
            const outputDevices = await client.getOutputDevices();
            if (cancelled) {
                return;
            }

                        const prefs = loadPreferences();
                        const inputId = prefs.inputDeviceId && inputDevices.some(d => d.deviceId === prefs.inputDeviceId) 
                            ? prefs.inputDeviceId 
                            : inputDevices[0]?.deviceId ?? "";
                        const outputId = prefs.outputDeviceId && outputDevices.some(d => d.deviceId === prefs.outputDeviceId)
                            ? prefs.outputDeviceId
                            : outputDevices[0]?.deviceId ?? "";
            
                        setDevices({
                            inputId,
                            outputId,
                            inputDevices,
                            outputDevices
                        });
        }

        bootstrap().catch((cause) => setError(cause instanceof Error ? cause.message : String(cause)));

        return () => {
            cancelled = true;
            clientRef.current?.disconnect();
        };
    }, []);

    useEffect(() => {
        function onKeyDown(event: KeyboardEvent) {
            if (!connected || event.repeat) {
                return;
            }

            if (event.code === "F1") {
                void startTalking("PL-A");
            }
            if (event.code === "F2") {
                void startTalking("PL-B");
            }
        }

        function onKeyUp(event: KeyboardEvent) {
            if (!connected) {
                return;
            }

            if (event.code === "F1") {
                void stopTalking("PL-A");
            }
            if (event.code === "F2") {
                void stopTalking("PL-B");
            }
        }

        window.addEventListener("keydown", onKeyDown);
        window.addEventListener("keyup", onKeyUp);
        return () => {
            window.removeEventListener("keydown", onKeyDown);
            window.removeEventListener("keyup", onKeyUp);
        };
    }, [connected, talkState]);

    useEffect(() => {
        if (!health.reconnectDelayMs) {
            return;
        }

        // Update reconnect countdown every 100ms
        const timer = setInterval(() => {
            setHealth((current) => ({ ...current }));
        }, 100);

        return () => clearInterval(timer);
    }, [health.reconnectDelayMs]);

    async function handleConnect() {
        if (!clientRef.current) {
            return;
        }

        setBusy(true);
        setError("");
        try {
            await navigator.mediaDevices.getUserMedia({ audio: true });
            await clientRef.current.login({ username, password });
            await clientRef.current.connect(channels);
            if (devices.inputId) {
                await clientRef.current.setInputDevice(devices.inputId);
            }
            if (devices.outputId) {
                await clientRef.current.setOutputDevice(devices.outputId);
            }
            setConnected(true);
        } catch (cause) {
            setError(cause instanceof Error ? cause.message : String(cause));
            setConnected(false);
        } finally {
            setBusy(false);
        }
    }

    function handleDisconnect() {
        clientRef.current?.disconnect();
        setConnected(false);
        setTalkState({ "PL-A": false, "PL-B": false });
    }

    async function startTalking(channel: ChannelId) {
        if (!clientRef.current || !connected) {
            return;
        }

        const next: Record<ChannelId, boolean> = { "PL-A": false, "PL-B": false, [channel]: true };
        setTalkState(next);
        for (const current of channels) {
            await clientRef.current.setTalking(current, current === channel);
        }
    }

    async function stopTalking(channel: ChannelId) {
        if (!clientRef.current || !connected) {
            return;
        }

        setTalkState((current) => ({ ...current, [channel]: false }));
        await clientRef.current.setTalking(channel, false);
    }

    async function toggleListen(channel: ChannelId) {
        if (!clientRef.current) {
            return;
        }

        const nextValue = !listenState[channel];
        setListenState((current) => ({ ...current, [channel]: nextValue }));
        await clientRef.current.setListening(channel, nextValue);
    }

    async function handleInputDeviceChange(deviceId: string) {
        setDevices((current) => ({ ...current, inputId: deviceId }));
        await clientRef.current?.setInputDevice(deviceId);
        const prefs = loadPreferences();
        savePreferences({ ...prefs, inputDeviceId: deviceId });
    }

    async function handleOutputDeviceChange(deviceId: string) {
        setDevices((current) => ({ ...current, outputId: deviceId }));
        await clientRef.current?.setOutputDevice(deviceId);
        const prefs = loadPreferences();
        savePreferences({ ...prefs, outputDeviceId: deviceId });
    }

    return (
        <main className="page">
            <section className="hero">
                <div>
                    <p className="eyebrow">Unity-ish Desktop</p>
                    <h1>Self-hosted intercom without the Unity engine</h1>
                    <p className="lede">Mac-first operator client with Windows portability, two party-lines, local-first auth, and LiveKit-backed voice transport.</p>
                </div>
                <div className={`health health-${health.status}`}>
                    <strong>{health.status.toUpperCase()}</strong>
                    <span>RTT {health.maxRttMs < 0 ? "-" : `${health.maxRttMs.toFixed(0)} ms`}</span>
                    <span>Loss {health.maxLossPercent < 0 ? "-" : `${health.maxLossPercent.toFixed(1)}%`}</span>
                    {health.reconnectAttempt ? (
                        <span className="reconnect">
                            Reconnect #{health.reconnectAttempt} in {(health.reconnectDelayMs ?? 0 / 1000).toFixed(1)}s
                        </span>
                    ) : null}
                </div>
            </section>

            <section className="grid">
                <article className="panel auth-panel">
                    <h2>Operator Login</h2>
                    <label>
                        Username
                        <input value={username} onChange={(event) => setUsername(event.target.value)} />
                    </label>
                    <label>
                        Password
                        <input type="password" value={password} onChange={(event) => setPassword(event.target.value)} />
                    </label>
                    <div className="button-row">
                        <button className="primary" onClick={handleConnect} disabled={busy || connected}>Connect</button>
                        <button className="ghost" onClick={handleDisconnect} disabled={!connected}>Disconnect</button>
                    </div>
                    {error ? <p className="error">{error}</p> : null}
                    <p className="hint">PTT hotkeys: F1 for PL-A, F2 for PL-B.</p>
                </article>

                <article className="panel device-panel">
                    <h2>Audio Devices</h2>
                    <label>
                        Input
                        <select value={devices.inputId} onChange={(event) => void handleInputDeviceChange(event.target.value)}>
                            {devices.inputDevices.map((device) => (
                                <option key={device.deviceId} value={device.deviceId}>{device.label || `Input ${device.deviceId.slice(0, 6)}`}</option>
                            ))}
                        </select>
                    </label>
                    <label>
                        Output
                        <select value={devices.outputId} onChange={(event) => void handleOutputDeviceChange(event.target.value)}>
                            {devices.outputDevices.map((device) => (
                                <option key={device.deviceId} value={device.deviceId}>{device.label || `Output ${device.deviceId.slice(0, 6)}`}</option>
                            ))}
                        </select>
                    </label>
                    <p className="hint">Output routing uses `setSinkId` when the host WebView supports it.</p>
                </article>
            </section>

            <section className="channels">
                {channels.map((channel) => (
                    <article key={channel} className={`channel-card ${talkState[channel] ? "talking" : ""}`}>
                        <header>
                            <div>
                                <p className="eyebrow">Party-line</p>
                                <h2>{channel}</h2>
                            </div>
                            <button className={listenState[channel] ? "primary" : "ghost"} onClick={() => void toggleListen(channel)}>
                                {listenState[channel] ? "Listening" : "Muted"}
                            </button>
                        </header>
                        <div className="button-row">
                            <button className="talk" disabled={!connected} onMouseDown={() => void startTalking(channel)} onMouseUp={() => void stopTalking(channel)} onMouseLeave={() => void stopTalking(channel)}>
                                {talkState[channel] ? "Talking" : "Hold To Talk"}
                            </button>
                        </div>
                    </article>
                ))}
            </section>

            <footer className="footer">
                <span>Platform: {window.unityIshDesktop?.platform ?? "browser"}</span>
                <span>Version: {window.unityIshDesktop?.version ?? "dev"}</span>
            </footer>
        </main>
    );
}
