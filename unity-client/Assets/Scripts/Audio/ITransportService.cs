using System.Threading.Tasks;

namespace UnityIsh.Audio
{
    /// <summary>
    /// Abstracts the realtime voice transport layer (backed by LiveKit WebRTC).
    /// Implement per platform; keep all SDK-specific code behind this interface.
    /// </summary>
    public interface ITransportService
    {
        /// <summary>Join a LiveKit room and begin subscribing to audio tracks.</summary>
        Task ConnectAsync(string channel, string livekitUrl, string token);

        /// <summary>Leave a LiveKit room and clean up resources.</summary>
        void Disconnect(string channel);

        /// <summary>Start or stop publishing the local microphone track on this channel.</summary>
        void SetPublishing(string channel, bool publishing);

        /// <summary>Subscribe to or unsubscribe from all remote audio on this channel.</summary>
        void SetSubscribeAll(string channel, bool subscribe);

        /// <summary>Latest round-trip time in milliseconds, or -1 if unknown.</summary>
        float GetLastRttMs(string channel);

        /// <summary>Latest packet-loss percentage (0-100), or -1 if unknown.</summary>
        float GetLastPacketLossPercent(string channel);
    }
}
