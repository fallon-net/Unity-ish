using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
// LiveKit Unity SDK — resolves after UPM fetches the manifest entry.
// Package entry: "io.livekit.unity": "https://github.com/livekit/client-sdk-unity.git?path=/Assets"
using LiveKit;

namespace UnityIsh.Audio
{
    /// <summary>
    /// LiveKit-backed transport implementation.
    /// One Room instance per party-line channel.
    /// All LiveKit types stay inside this class — nothing above this adapter imports LiveKit directly.
    /// WIRING: attach to the same GameObject as IntercomController.
    /// </summary>
    public sealed class LiveKitTransportService : MonoBehaviour, ITransportService
    {
        private readonly Dictionary<string, Room> _rooms = new();
        private readonly Dictionary<string, ChannelState> _state = new();

        private sealed class ChannelState
        {
            public bool IsPublishing;
            public float LastRttMs = -1f;
            public float LastLossPercent = -1f;
        }

        public async Task ConnectAsync(string channel, string livekitUrl, string roomToken)
        {
            if (_rooms.TryGetValue(channel, out var existing))
            {
                existing.Disconnect();
                _rooms.Remove(channel);
                _state.Remove(channel);
            }

            var room = new Room();
            var state = new ChannelState();

            room.LocalParticipantConnectionQualityChanged += quality =>
            {
                (state.LastRttMs, state.LastLossPercent) = QualityToMetrics(quality);
                Debug.Log($"[LiveKit:{channel}] Quality={quality} rtt~{state.LastRttMs}ms loss~{state.LastLossPercent}%");
            };

            room.Disconnected += () =>
            {
                Debug.LogWarning($"[LiveKit:{channel}] Room disconnected");
                state.LastRttMs = 999f; // triggers ConnectedBad -> reconnect loop
            };

            _rooms[channel] = room;
            _state[channel] = state;

            await room.Connect(livekitUrl, roomToken,
                new ConnectOptions { AutoSubscribe = true },
                new RoomOptions { AudioEnabled = true });

            Debug.Log($"[LiveKit:{channel}] Connected to room ({room.Name})");
        }

        public void Disconnect(string channel)
        {
            if (_rooms.TryGetValue(channel, out var room))
            {
                room.Disconnect();
                _rooms.Remove(channel);
            }
            _state.Remove(channel);
            Debug.Log($"[LiveKit:{channel}] Disconnected");
        }

        public void SetPublishing(string channel, bool publishing)
        {
            if (!_rooms.TryGetValue(channel, out var room)) return;
            if (!_state.TryGetValue(channel, out var state)) return;
            if (publishing == state.IsPublishing) return;

            state.IsPublishing = publishing;
            room.LocalParticipant.SetMicrophoneEnabled(publishing);
            Debug.Log($"[LiveKit:{channel}] Mic publishing={publishing}");
        }

        public void SetSubscribeAll(string channel, bool subscribe)
        {
            if (!_rooms.TryGetValue(channel, out var room)) return;

            foreach (var participant in room.RemoteParticipants.Values)
                foreach (var pub in participant.TrackPublications.Values)
                    if (pub.Kind == TrackKind.Audio)
                        pub.SetSubscribed(subscribe);

            Debug.Log($"[LiveKit:{channel}] Remote audio subscribe={subscribe}");
        }

        public float GetLastRttMs(string channel)
            => _state.TryGetValue(channel, out var s) ? s.LastRttMs : -1f;

        public float GetLastPacketLossPercent(string channel)
            => _state.TryGetValue(channel, out var s) ? s.LastLossPercent : -1f;

        // Translate LiveKit ConnectionQuality -> approximate metrics for health thresholds.
        private static (float rttMs, float lossPercent) QualityToMetrics(ConnectionQuality q)
            => q switch
            {
                ConnectionQuality.Excellent => (40f, 0f),
                ConnectionQuality.Good => (100f, 1.5f),
                ConnectionQuality.Poor => (200f, 8f),
                _ => (-1f, -1f)
            };
    }
}
