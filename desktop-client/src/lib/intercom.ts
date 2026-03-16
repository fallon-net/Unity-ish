import {
    Room,
    RoomEvent,
    Track,
    type ConnectionQuality,
    type RoomConnectOptions,
    type RoomOptions,
    type RemoteTrack,
    type RemoteTrackPublication
} from "livekit-client";

export type ChannelId = "PL-A" | "PL-B";

export interface LoginPayload {
    username: string;
    password: string;
}

export interface ClientConfig {
    controlApiBaseUrl: string;
    livekitUrl?: string;
    defaultListen?: Record<string, boolean>;
}

export interface HealthSnapshot {
    status: "disconnected" | "good" | "warn" | "bad";
    maxRttMs: number;
    maxLossPercent: number;
    reconnectAttempt?: number;
    reconnectDelayMs?: number;
}

interface LoginResponse {
    accessToken: string;
}

interface TokenResponse {
    livekitUrl: string;
    tokens: Record<string, string>;
}

interface ChannelState {
    room: Room;
    audioElements: Set<HTMLAudioElement>;
    lastRttMs: number;
    lastLossPercent: number;
}

interface ReconnectState {
    attempt: number;
    maxAttempts: number;
    baseDelayMs: number;
    maxDelayMs: number;
    nextRetryAt?: number;
    reconnectTimeoutId?: NodeJS.Timeout;
}

const channels: ChannelId[] = ["PL-A", "PL-B"];

export class IntercomClient {
    private readonly config: ClientConfig;
    private appToken = "";
    private readonly channelStates = new Map<ChannelId, ChannelState>();
    private onHealthChanged?: (health: HealthSnapshot) => void;
    private reconnectState: ReconnectState = {
        attempt: 0,
        maxAttempts: 15,
        baseDelayMs: 1000,
        maxDelayMs: 60000
    };
    private lastConnectPayload?: { canTalk: ChannelId[] };

    constructor(config: ClientConfig, onHealthChanged?: (health: HealthSnapshot) => void) {
        this.config = config;
        this.onHealthChanged = onHealthChanged;
    }

    async login(payload: LoginPayload): Promise<void> {
        const result = await this.postJson<LoginResponse>("/v1/auth/login", payload);
        this.appToken = result.accessToken;
    }

    async connect(canTalk: ChannelId[] = channels): Promise<void> {
        if (!this.appToken) {
            throw new Error("login required before connect");
        }

        this.lastConnectPayload = { canTalk };
        this.reconnectState.attempt = 0;
        this.clearReconnectTimer();

        try {
            await this.performConnect(canTalk);
        } catch (error) {
            console.error("Connection failed:", error);
            this.scheduleReconnect();
            this.emitHealth();
        }
    }

    private async performConnect(canTalk: ChannelId[]): Promise<void> {
        const tokenResponse = await this.postJson<TokenResponse>(
            "/v1/token/livekit",
            { channels, canTalk },
            this.appToken
        );

        await Promise.all(
            channels.map(async (channel) => {
                const existing = this.channelStates.get(channel);
                if (existing) {
                    existing.room.disconnect();
                    existing.audioElements.forEach((element) => element.remove());
                    this.channelStates.delete(channel);
                }

                const room = new Room(this.roomOptions());
                const state: ChannelState = {
                    room,
                    audioElements: new Set(),
                    lastRttMs: -1,
                    lastLossPercent: -1
                };

                this.bindRoomEvents(channel, state);
                this.channelStates.set(channel, state);
                await room.connect(
                    tokenResponse.livekitUrl || this.config.livekitUrl || "ws://127.0.0.1:7880",
                    tokenResponse.tokens[channel],
                    this.connectOptions()
                );
            })
        );

        this.emitHealth();
    }

    disconnect(): void {
        this.clearReconnectTimer();
        this.lastConnectPayload = undefined;
        for (const state of this.channelStates.values()) {
            state.room.disconnect();
            state.audioElements.forEach((element) => element.remove());
        }
        this.channelStates.clear();
        this.reconnectState.attempt = 0;
        this.emitHealth();
    }

    private clearReconnectTimer(): void {
        if (this.reconnectState.reconnectTimeoutId) {
            clearTimeout(this.reconnectState.reconnectTimeoutId);
            this.reconnectState.reconnectTimeoutId = undefined;
            this.reconnectState.nextRetryAt = undefined;
        }
    }

    private scheduleReconnect(): void {
        if (!this.lastConnectPayload) {
            console.log("No connection payload; not scheduling reconnect");
            return;
        }

        if (this.reconnectState.attempt >= this.reconnectState.maxAttempts) {
            console.error("Max reconnection attempts reached");
            return;
        }

        // Exponential backoff with jitter: delay = min(baseDelay * 2^attempt * random(0.5, 1.5), maxDelay)
        const exponentialDelay = this.reconnectState.baseDelayMs * Math.pow(2, this.reconnectState.attempt);
        const jitteredDelay = exponentialDelay * (0.5 + Math.random());
        const delayMs = Math.min(jitteredDelay, this.reconnectState.maxDelayMs);

        this.reconnectState.attempt++;
        this.reconnectState.nextRetryAt = Date.now() + delayMs;

        console.log(`Scheduling reconnect attempt ${this.reconnectState.attempt}/${this.reconnectState.maxAttempts} in ${delayMs.toFixed(0)}ms`);

        this.reconnectState.reconnectTimeoutId = setTimeout(async () => {
            console.log(`Attempting reconnect #${this.reconnectState.attempt}...`);
            try {
                const payload = this.lastConnectPayload!;
                await this.performConnect(payload.canTalk);
                this.reconnectState.attempt = 0;
                console.log("Reconnection successful");
            } catch (error) {
                console.error("Reconnection failed:", error);
                this.scheduleReconnect();
            } finally {
                this.emitHealth();
            }
        }, delayMs);
    }

    async setInputDevice(deviceId: string): Promise<void> {
        await Promise.all(Array.from(this.channelStates.values()).map((state) => state.room.switchActiveDevice("audioinput", deviceId)));
    }

    async setOutputDevice(deviceId: string): Promise<void> {
        const supportsSinkId = typeof HTMLMediaElement !== "undefined" && "setSinkId" in HTMLMediaElement.prototype;
        if (!supportsSinkId) {
            return;
        }

        await Promise.all(
            Array.from(this.channelStates.values()).flatMap((state) =>
                Array.from(state.audioElements).map((element) => (element as HTMLAudioElement & { setSinkId?(id: string): Promise<void> }).setSinkId?.(deviceId) ?? Promise.resolve())
            )
        );
    }

    async setListening(channel: ChannelId, listening: boolean): Promise<void> {
        const state = this.channelStates.get(channel);
        if (!state) {
            return;
        }

        for (const participant of state.room.remoteParticipants.values()) {
            for (const publication of participant.trackPublications.values()) {
                if (publication.kind === Track.Kind.Audio) {
                    await publication.setSubscribed(listening);
                }
            }
        }
    }

    async setTalking(channel: ChannelId, talking: boolean): Promise<void> {
        const state = this.channelStates.get(channel);
        if (!state) {
            return;
        }

        await state.room.localParticipant.setMicrophoneEnabled(talking);
    }

    async getInputDevices(): Promise<MediaDeviceInfo[]> {
        return this.getDevices("audioinput");
    }

    async getOutputDevices(): Promise<MediaDeviceInfo[]> {
        return this.getDevices("audiooutput");
    }

    private bindRoomEvents(channel: ChannelId, state: ChannelState): void {
        state.room.on(RoomEvent.TrackSubscribed, (track: RemoteTrack, publication: RemoteTrackPublication) => {
            if (track.kind !== Track.Kind.Audio) {
                return;
            }

            const element = track.attach() as HTMLAudioElement;
            element.autoplay = true;
            element.dataset.channel = channel;
            element.dataset.trackSid = publication.trackSid;
            element.style.display = "none";
            document.body.appendChild(element);
            state.audioElements.add(element);
        });

        state.room.on(RoomEvent.TrackUnsubscribed, (track: RemoteTrack) => {
            if (track.kind !== Track.Kind.Audio) {
                return;
            }

            const detached = track.detach();
            detached.forEach((node) => {
                const audio = node as HTMLAudioElement;
                state.audioElements.delete(audio);
                audio.remove();
            });
        });

        state.room.on(RoomEvent.ConnectionQualityChanged, (quality: ConnectionQuality) => {
            const metrics = this.qualityToMetrics(quality);
            state.lastRttMs = metrics.rttMs;
            state.lastLossPercent = metrics.lossPercent;
            this.emitHealth();
        });

        state.room.on(RoomEvent.Disconnected, () => {
            state.lastRttMs = 999;
            state.lastLossPercent = 100;

            // Check if all rooms are disconnected
            const allDisconnected = Array.from(this.channelStates.values()).every(
                s => s.room.remoteParticipants.size === 0 && !s.audioElements.size
            );

            if (allDisconnected && this.lastConnectPayload) {
                console.log("All channels disconnected; scheduling reconnect");
                this.scheduleReconnect();
            }

            this.emitHealth();
        });
    }

    private emitHealth(): void {
        if (!this.onHealthChanged) {
            return;
        }

        if (this.channelStates.size === 0) {
            const snapshot: HealthSnapshot = { status: "disconnected", maxRttMs: -1, maxLossPercent: -1 };
            if (this.reconnectState.nextRetryAt) {
                snapshot.reconnectAttempt = this.reconnectState.attempt;
                snapshot.reconnectDelayMs = Math.max(0, this.reconnectState.nextRetryAt - Date.now());
            }
            this.onHealthChanged(snapshot);
            return;
        }

        let maxRttMs = 0;
        let maxLossPercent = 0;
        for (const state of this.channelStates.values()) {
            maxRttMs = Math.max(maxRttMs, state.lastRttMs);
            maxLossPercent = Math.max(maxLossPercent, state.lastLossPercent);
        }

        let status: HealthSnapshot["status"] = "good";
        if (maxRttMs > 150 || maxLossPercent > 5) {
            status = "bad";
        } else if (maxRttMs > 80 || maxLossPercent > 2) {
            status = "warn";
        }

        const snapshot: HealthSnapshot = { status, maxRttMs, maxLossPercent };
        if (this.reconnectState.nextRetryAt) {
            snapshot.reconnectAttempt = this.reconnectState.attempt;
            snapshot.reconnectDelayMs = Math.max(0, this.reconnectState.nextRetryAt - Date.now());
        }

        this.onHealthChanged(snapshot);
    }

    private roomOptions(): RoomOptions {
        return {
            adaptiveStream: true,
            dynacast: true
        };
    }

    private connectOptions(): RoomConnectOptions {
        return {
            autoSubscribe: true
        };
    }

    private qualityToMetrics(quality: ConnectionQuality): { rttMs: number; lossPercent: number } {
        switch (quality) {
            case "excellent":
                return { rttMs: 40, lossPercent: 0 };
            case "good":
                return { rttMs: 100, lossPercent: 1.5 };
            case "poor":
                return { rttMs: 220, lossPercent: 8 };
            default:
                return { rttMs: -1, lossPercent: -1 };
        }
    }

    private async getDevices(kind: MediaDeviceKind): Promise<MediaDeviceInfo[]> {
        if (!navigator.mediaDevices?.enumerateDevices) {
            return [];
        }

        const devices = await navigator.mediaDevices.enumerateDevices();
        return devices.filter((device) => device.kind === kind);
    }

    private async postJson<T>(path: string, body: unknown, bearerToken?: string): Promise<T> {
        const response = await fetch(`${this.config.controlApiBaseUrl}${path}`, {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                ...(bearerToken ? { Authorization: `Bearer ${bearerToken}` } : {})
            },
            body: JSON.stringify(body)
        });

        if (!response.ok) {
            const text = await response.text();
            throw new Error(text || `request failed: ${response.status}`);
        }

        return response.json() as Promise<T>;
    }
}

export async function loadClientConfig(): Promise<ClientConfig> {
    const response = await fetch("/config/client.sample.json");
    if (!response.ok) {
        return { controlApiBaseUrl: "http://127.0.0.1:8080" };
    }

    return response.json() as Promise<ClientConfig>;
}
