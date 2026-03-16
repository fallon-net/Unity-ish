using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityIsh.Audio;
using UnityIsh.Core.State;
using Newtonsoft.Json;
using UnityIsh.UI;

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
        [SerializeField] private LoginPanel loginPanel;

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
            var path = System.IO.Path.Combine(Application.persistentDataPath, configFileName);
            if (System.IO.File.Exists(path))
            {
                try
                {
                    var cfg = JsonConvert.DeserializeObject<ClientConfig>(System.IO.File.ReadAllText(path));
                    _controlApiBase = cfg?.ControlApiBaseUrl ?? "http://127.0.0.1:8080";
                    _livekitUrl = cfg?.LivekitUrl ?? "ws://127.0.0.1:7880";
                    Debug.Log($"[IntercomController] Config loaded from {path}");
                    return;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[IntercomController] Config parse failed, using defaults: {ex.Message}");
                }
            }
            _controlApiBase = "http://127.0.0.1:8080";
            _livekitUrl = "ws://127.0.0.1:7880";
            Debug.Log("[IntercomController] No config file found, using defaults");
        }

        /// <summary>POST /v1/auth/login and store the resulting app token.</summary>
        private async Task LoginAsync()
        {
            var (username, password) = await WaitForLoginAsync();
            var body = $"{{\"username\":\"{EscapeJson(username)}\",\"password\":\"{EscapeJson(password)}\"}}";
            var result = await PostJsonAsync($"{_controlApiBase}/v1/auth/login", body);
            var parsed = JsonConvert.DeserializeObject<LoginResponse>(result);
            if (parsed == null || string.IsNullOrEmpty(parsed.AccessToken))
            {
                loginPanel?.ShowError("Login failed. Check credentials.");
                throw new Exception("login failed: empty token");
            }
            loginPanel?.Hide();
            _appToken = parsed.AccessToken;
            Debug.Log("[IntercomController] Login successful");
        }

        /// <summary>POST /v1/token/livekit and connect both party-line channels.</summary>
        private async Task FetchAndConnectAsync()
        {
            var body = $"{{\"channels\":[\"PL-A\",\"PL-B\"],\"canTalk\":[\"PL-A\",\"PL-B\"]}}";
            var result = await PostJsonAsync($"{_controlApiBase}/v1/token/livekit", body, _appToken);
            var parsed = JsonConvert.DeserializeObject<TokenResponse>(result);
            if (parsed?.Tokens == null)
                throw new Exception("token response missing");

            foreach (var ch in Channels)
            {
                _partyLines.Register(ch, ch, canTalk: true);
                if (!parsed.Tokens.TryGetValue(ch, out var roomToken))
                    throw new Exception($"no token for channel {ch}");
                await _transport.ConnectAsync(ch, parsed.LivekitUrl, roomToken);
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

        // ---- Login UI helper ------------------------------------------------

        private TaskCompletionSource<(string, string)> _loginTcs;

        private Task<(string, string)> WaitForLoginAsync()
        {
            _loginTcs = new TaskCompletionSource<(string, string)>();
            if (loginPanel != null)
            {
                loginPanel.Show();
                loginPanel.OnLoginSubmit += (u, p) => _loginTcs.TrySetResult((u, p));
            }
            else
            {
                // Headless fallback — check env / default.
                _loginTcs.SetResult(("ops", "changeme"));
            }
            return _loginTcs.Task;
        }

        // ---- HTTP Helpers ---------------------------------------------------

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

        private static string EscapeJson(string s)
            => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

        // ---- JSON models ----------------------------------------------------

        private sealed class ClientConfig
        {
            [JsonProperty("controlApiBaseUrl")] public string ControlApiBaseUrl;
            [JsonProperty("livekitUrl")] public string LivekitUrl;
        }

        private sealed class LoginResponse
        {
            [JsonProperty("accessToken")] public string AccessToken;
        }

        private sealed class TokenResponse
        {
            [JsonProperty("livekitUrl")] public string LivekitUrl;
            [JsonProperty("tokens")] public Dictionary<string, string> Tokens;
        }
    }
}
