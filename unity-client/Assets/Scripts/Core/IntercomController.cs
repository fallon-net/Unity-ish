using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityIsh.Audio;
using UnityIsh.Core.State;

namespace UnityIsh.Core
{
    /// <summary>
    /// Main MonoBehaviour orchestrator for the Unity-ish intercom client.
    ///
    /// Flow:
    ///   1. Load config from persistent data path.
    ///   2. Login to control API -> receive app JWT.
    ///   3. Exchange app JWT for LiveKit room tokens (PL-A, PL-B).
    ///   4. Connect transport to both party-line rooms.
    ///   5. Poll PTT input each frame; drive party-line service.
    ///   6. Monitor connection health; trigger reconnect when needed.
    ///
    /// Wire-up in the Unity Inspector:
    ///   - Set TransportService to a GameObject with LiveKitTransportService.
    ///   - Set AudioDeviceService to the platform adapter component.
    ///   - Assign PttInputService.
    ///   - Assign ReconnectService (on same GameObject or child).
    /// </summary>
    public sealed class IntercomController : MonoBehaviour
    {
        [Header("Service References")]
        [SerializeField] private MonoBehaviour transportServiceRef;
        [SerializeField] private MonoBehaviour audioDeviceServiceRef;
        [SerializeField] private MonoBehaviour pttInputServiceRef;
        [SerializeField] private ReconnectService reconnectService;

        [Header("Config Path (relative to persistentDataPath)")]
        [SerializeField] private string configFileName = "client.json";

        // Runtime state
        private ITransportService _transport;
        private IAudioDeviceService _audioDevices;
        private Input.IPttInputService _ptt;
        private PartyLineService _partyLines;
        private ConnectionStateMachine _stateMachine;

        private string _controlApiBase;
        private string _livekitUrl;
        private string _appToken;
        private bool _isReconnecting;

        private static readonly string[] Channels = { "PL-A", "PL-B" };

        // ---- Unity lifecycle -----------------------------------------------

        private void Awake()
        {
            _transport = (ITransportService)transportServiceRef;
            _audioDevices = (IAudioDeviceService)audioDeviceServiceRef;
            _ptt = (Input.IPttInputService)pttInputServiceRef;
            _partyLines = new PartyLineService(_transport);
            _stateMachine = new ConnectionStateMachine();
            _stateMachine.OnChanged += OnConnectionStateChanged;
        }

        private void Start()
        {
            _ = StartupAsync();
        }

        private void Update()
        {
            if (_stateMachine.Current != ConnectionState.ConnectedGood &&
                _stateMachine.Current != ConnectionState.ConnectedWarn)
                return;

            // PTT per channel.
            foreach (var ch in Channels)
            {
                if (_ptt.IsPressed(ch))
                    _partyLines.PttDown(ch);
                else
                    _partyLines.PttUp(ch);
            }

            UpdateHealthState();
        }

        private void OnDestroy()
        {
            foreach (var ch in Channels) _transport.Disconnect(ch);
        }

        // ---- Startup sequence -----------------------------------------------

        private async Task StartupAsync()
        {
            LoadConfig();
            _stateMachine.Transition(ConnectionState.Connecting);

            try
            {
                await LoginAsync();
                await FetchAndConnectAsync();
                reconnectService.Reset();
                _stateMachine.Transition(ConnectionState.ConnectedGood);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[IntercomController] Startup failed: {ex.Message}");
                StartCoroutine(ReconnectLoop());
            }
        }

        private void LoadConfig()
        {
            // TODO: read client.json via UnityEngine.Application.persistentDataPath
            // and parse into a config struct. Using defaults for now.
            _controlApiBase = "http://127.0.0.1:8080";
            _livekitUrl = "ws://127.0.0.1:7880";
            Debug.Log("[IntercomController] Config loaded (defaults)");
        }

        /// <summary>POST /v1/auth/login and store the resulting app token.</summary>
        private async Task LoginAsync()
        {
            // TODO: replace player_prefs lookup with a proper login UI.
            const string username = "ops";
            const string password = "changeme";

            var body = $"{{\"username\":\"{username}\",\"password\":\"{password}\"}}";
            var result = await PostJsonAsync($"{_controlApiBase}/v1/auth/login", body);
            var parsed = JsonUtility.FromJson<LoginResponse>(result);

            if (string.IsNullOrEmpty(parsed.accessToken))
                throw new Exception("login failed: empty token");

            _appToken = parsed.accessToken;
            Debug.Log("[IntercomController] Login successful");
        }

        /// <summary>POST /v1/token/livekit and connect both party-line channels.</summary>
        private async Task FetchAndConnectAsync()
        {
            var body = $"{{\"channels\":[\"PL-A\",\"PL-B\"],\"canTalk\":[\"PL-A\",\"PL-B\"]}}";
            var result = await PostJsonAsync($"{_controlApiBase}/v1/token/livekit", body, _appToken);
            var parsed = JsonUtility.FromJson<TokenResponse>(result);

            // Register and connect each channel.
            foreach (var ch in Channels)
            {
                _partyLines.Register(ch, ch, canTalk: true);
                var roomToken = GetTokenForChannel(parsed, ch);
                await _transport.ConnectAsync(ch, parsed.livekitUrl, roomToken);
            }

            Debug.Log("[IntercomController] Both channels connected");
        }

        // ---- Reconnect ------------------------------------------------------

        private IEnumerator ReconnectLoop()
        {
            while (true)
            {
                _isReconnecting = true;
                _stateMachine.Transition(ConnectionState.Reconnecting);
                yield return reconnectService.Wait();

                bool ok = false;
                var task = StartupAsync();
                yield return new WaitUntil(() => task.IsCompleted);
                ok = task.IsCompletedSuccessfully;

                if (ok)
                {
                    _isReconnecting = false;
                    yield break;
                }
            }
        }

        // ---- Health ---------------------------------------------------------

        private void UpdateHealthState()
        {
            float maxRtt = 0f, maxLoss = 0f;
            foreach (var ch in Channels)
            {
                float rtt = _transport.GetLastRttMs(ch);
                float loss = _transport.GetLastPacketLossPercent(ch);
                if (rtt > maxRtt) maxRtt = rtt;
                if (loss > maxLoss) maxLoss = loss;
            }

            var next = EvaluateHealth(maxRtt, maxLoss);
            _stateMachine.Transition(next);
        }

        private static ConnectionState EvaluateHealth(float rttMs, float lossPercent)
        {
            if (rttMs > 150f || lossPercent > 5f) return ConnectionState.ConnectedBad;
            if (rttMs > 80f || lossPercent > 2f) return ConnectionState.ConnectedWarn;
            return ConnectionState.ConnectedGood;
        }

        private void OnConnectionStateChanged(ConnectionState state)
        {
            Debug.Log($"[IntercomController] State -> {state}");
            // TODO: update UI status badge.
        }

        // ---- HTTP Helpers (UnityWebRequest) ---------------------------------

        private static async Task<string> PostJsonAsync(string url, string json, string bearerToken = null)
        {
            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            if (!string.IsNullOrEmpty(bearerToken))
                req.SetRequestHeader("Authorization", $"Bearer {bearerToken}");

            var op = req.SendWebRequest();
            while (!op.isDone)
                await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success)
                throw new Exception($"HTTP {req.responseCode}: {req.error}");

            return req.downloadHandler.text;
        }

        // ---- JSON shims (avoid JSON.NET dependency for this stub) -----------

        [Serializable] private class LoginResponse { public string accessToken; }

        [Serializable]
        private class TokenResponse
        {
            public string livekitUrl;
            // Unity's JsonUtility doesn't support Dictionary; parse with a known key count.
            public string tokenPLA;
            public string tokenPLB;
        }

        private static string GetTokenForChannel(TokenResponse r, string ch) => ch switch
        {
            "PL-A" => r.tokenPLA,
            "PL-B" => r.tokenPLB,
            _ => throw new ArgumentException($"Unknown channel {ch}")
        };
    }
}
