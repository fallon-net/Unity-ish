using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace UnityIsh.Audio
{
    /// <summary>
    /// LiveKit-backed transport implementation stub.
    ///
    /// SETUP:
    ///   1. Add the LiveKit Unity SDK package to your project.
    ///      https://github.com/livekit/client-sdk-unity
    ///   2. Replace each TODO block below with the matching LiveKit SDK call.
    ///   3. Keep all LiveKit types inside this class — nothing above this
    ///      adapter should import LiveKit directly.
    /// </summary>
    public sealed class LiveKitTransportService : MonoBehaviour, ITransportService
    {
        // TODO: declare per-channel Room instances when LiveKit SDK is present.
        // private readonly Dictionary<string, LiveKit.Room> _rooms = new();

        private readonly Dictionary<string, ChannelState> _state = new();

        private sealed class ChannelState
        {
            public bool IsConnected;
            public bool IsPublishing;
            public float LastRttMs = -1f;
            public float LastLossPercent = -1f;
        }

        public async Task ConnectAsync(string channel, string livekitUrl, string token)
        {
            if (!_state.ContainsKey(channel))
            {
                _state[channel] = new ChannelState();
            }

            // TODO: create and connect a LiveKit Room.
            // var room = new LiveKit.Room();
            // await room.ConnectAsync(livekitUrl, token, new RoomOptions { ... });
            // room.TrackSubscribed += OnTrackSubscribed;
            // room.Disconnected += (reason) => OnDisconnected(channel, reason);
            // _rooms[channel] = room;

            _state[channel].IsConnected = true;
            Debug.Log($"[LiveKitTransportService] Connected channel {channel} (stub)");
            await Task.CompletedTask;
        }

        public void Disconnect(string channel)
        {
            // TODO: _rooms[channel]?.Disconnect();
            if (_state.TryGetValue(channel, out var s))
            {
                s.IsConnected = false;
                s.IsPublishing = false;
            }
            Debug.Log($"[LiveKitTransportService] Disconnected channel {channel}");
        }

        public void SetPublishing(string channel, bool publishing)
        {
            if (!_state.TryGetValue(channel, out var s) || !s.IsConnected) return;

            // TODO: get local audio track from _rooms[channel] and mute/unmute.
            // var track = _rooms[channel].LocalParticipant.AudioTracks.FirstOrDefault();
            // track?.SetMuted(!publishing);

            s.IsPublishing = publishing;
            Debug.Log($"[LiveKitTransportService] Channel {channel} publishing={publishing}");
        }

        public void SetSubscribeAll(string channel, bool subscribe)
        {
            if (!_state.TryGetValue(channel, out var s) || !s.IsConnected) return;

            // TODO: iterate remote participants and set audio track subscription.
            // foreach (var p in _rooms[channel].RemoteParticipants.Values)
            //     foreach (var t in p.AudioTracks.Values)
            //         t.SetSubscribed(subscribe);

            Debug.Log($"[LiveKitTransportService] Channel {channel} subscribe={subscribe}");
        }

        public float GetLastRttMs(string channel)
            => _state.TryGetValue(channel, out var s) ? s.LastRttMs : -1f;

        public float GetLastPacketLossPercent(string channel)
            => _state.TryGetValue(channel, out var s) ? s.LastLossPercent : -1f;

        // TODO: subscribe to LiveKit Room stats events and update _state[channel].LastRttMs
        // and _state[channel].LastLossPercent from RtcStats.
    }
}
