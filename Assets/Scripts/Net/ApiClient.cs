using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace Zimo.Net
{
    /// <summary>
    /// Thin coroutine wrapper over the games backend REST API. Reads the API
    /// base + player token from <see cref="WebBridge"/>. All game authority is
    /// server-side; this just fetches state and posts bets.
    ///
    /// Attach to a persistent GameObject and reference it from GameManager.
    /// </summary>
    public class ApiClient : MonoBehaviour
    {
        [Tooltip("Game slug — must match the backend GameConfig.gameKey and the " +
                 "WebGL folder served at /<gameKey>/.")]
        public string gameKey = "ferris_wheel";

        [Header("Editor / dev testing only (ignored in WebGL builds)")]
        [Tooltip("API base used when running in the Editor. In a real WebGL " +
                 "build the `api` URL param from the WebView wins instead.")]
        public string devApiBase = "http://127.0.0.1:5002/api/v1";

        [Tooltip("Paste a player access token here to test SERVER mode in the " +
                 "Editor. Get one from POST /auth/login/email on the main " +
                 "backend. Leave blank to run the offline demo. Ignored in WebGL.")]
        [TextArea(2, 4)]
        public string devToken = "";

        void Awake()
        {
            // Feed the Editor/dev fallbacks into WebBridge so GameManager sees a
            // session in the Editor. In WebGL these are overridden by the real
            // URL params (token / api) the Flutter host appends.
            if (!string.IsNullOrEmpty(devApiBase))
                WebBridge.EditorApiBase = NormalizeLoopback(devApiBase);
            if (!string.IsNullOrEmpty(devToken)) WebBridge.EditorToken = devToken.Trim();

            _tokenExp = ParseExp(Token);
        }

        /// <summary>
        /// macOS often resolves "localhost" to IPv6 (::1) while Node may be
        /// listening on IPv4 only. Pinning the loopback address avoids that.
        /// </summary>
        static string NormalizeLoopback(string url)
        {
            if (string.IsNullOrEmpty(url)) return url;
            return url.Replace("://localhost", "://127.0.0.1");
        }

        string Base => WebBridge.ApiBase.TrimEnd('/');
        string Token => WebBridge.Token;

        // ── Sliding session ──────────────────────────────────────────────────
        //
        // The launch token lives ~15 minutes. The backend mints a fresh one from
        // a still-valid (or recently expired) token via POST /games/session/extend,
        // up to the 8-hour cap on the ORIGINAL login. So we refresh a couple of
        // minutes before expiry and, as a safety net, retry any 401 exactly once
        // after refreshing. The player keeps playing for hours without relaunching.

        /// <summary>Refresh when fewer than this many seconds of life remain.</summary>
        const double RefreshBeforeExpirySeconds = 120;

        /// <summary>How often the proactive check runs (cheap — no network).</summary>
        const float ExpiryCheckIntervalSeconds = 15f;

        /// <summary>Unix seconds at which the current token dies. 0 = unknown.</summary>
        double _tokenExp;

        bool _refreshing;
        bool _refreshOk;
        float _nextExpiryCheck;

        /// <summary>Raised when the session is over for good (8-hour cap reached, or
        /// the token was dead too long to revive). The game should stop polling and
        /// ask the player to relaunch. Never fires for transient network errors.</summary>
        public event Action<string> OnSessionEnded;

        /// <summary>True once <see cref="OnSessionEnded"/> has fired.</summary>
        public bool SessionEnded { get; private set; }

        /// <summary>Seconds of life left in the current token; double.MaxValue when
        /// there is no token (offline demo) or it carries no `exp`.</summary>
        public double SecondsUntilTokenExpiry()
        {
            if (_tokenExp <= 0) return double.MaxValue;
            var now = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            return _tokenExp - now;
        }

        void Update()
        {
            if (SessionEnded || _refreshing || string.IsNullOrEmpty(Token)) return;
            if (Time.unscaledTime < _nextExpiryCheck) return;
            _nextExpiryCheck = Time.unscaledTime + ExpiryCheckIntervalSeconds;

            if (SecondsUntilTokenExpiry() < RefreshBeforeExpirySeconds)
                StartCoroutine(Refresh(null));
        }

        /// <summary>Exchange the current token for a fresh one. Safe to call at any
        /// time; concurrent calls collapse into a single request.</summary>
        public Coroutine ExtendSession(Action onOk = null, Action<string> onErr = null) =>
            StartCoroutine(Refresh(ok =>
            {
                if (ok) onOk?.Invoke();
                else onErr?.Invoke("Could not extend the game session");
            }));

        IEnumerator Refresh(Action<bool> done)
        {
            // Single-flight: a second caller waits for the request already in flight.
            if (_refreshing)
            {
                while (_refreshing) yield return null;
                done?.Invoke(_refreshOk);
                yield break;
            }
            if (SessionEnded) { done?.Invoke(false); yield break; }

            _refreshing = true;
            _refreshOk = false;

            // No gameKey in this path — the session belongs to the player, not a game.
            using (var req = new UnityWebRequest($"{Base}/games/session/extend", "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes("{}"));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                ApplyHeaders(req);
                yield return req.SendWebRequest();

                var raw = req.downloadHandler != null ? req.downloadHandler.text : string.Empty;
                if (req.result == UnityWebRequest.Result.Success)
                {
                    string token = null;
                    try
                    {
                        var env = JsonUtility.FromJson<SessionEnvelope>(raw);
                        if (env != null && env.data != null) token = env.data.token;
                    }
                    catch { /* malformed body — counts as a failed refresh */ }

                    if (!string.IsNullOrEmpty(token))
                    {
                        WebBridge.SetToken(token);
                        _tokenExp = ParseExp(token);
                        _refreshOk = true;
                        Debug.Log($"[ApiClient] session extended — {Math.Round(SecondsUntilTokenExpiry())}s of life");
                    }
                }
                else
                {
                    // SESSION_EXPIRED = the 8-hour cap on the original login.
                    // TOKEN_TOO_OLD  = we came back long after the token died (a
                    //                  slept tab). Neither is recoverable without
                    //                  a fresh launch from the app.
                    string code = null, message = req.error ?? "Session extend failed";
                    try
                    {
                        var err = JsonUtility.FromJson<ErrorEnvelope>(raw);
                        if (err != null && err.error != null)
                        {
                            code = err.error.code;
                            if (!string.IsNullOrEmpty(err.error.message)) message = err.error.message;
                        }
                    }
                    catch { /* non-JSON error body */ }

                    if (code == "SESSION_EXPIRED" || code == "TOKEN_TOO_OLD")
                    {
                        SessionEnded = true;
                        Debug.LogWarning($"[ApiClient] session over: {code} — {message}");
                        OnSessionEnded?.Invoke(message);
                    }
                    else
                    {
                        Debug.LogWarning($"[ApiClient] session extend failed: {message}");
                    }
                }
            }

            _refreshing = false;
            done?.Invoke(_refreshOk);
        }

        /// <summary>Reads the `exp` claim out of a JWT without verifying it — the
        /// server is the only authority; this only decides when to refresh.
        /// Returns 0 when the token is absent or unparseable.</summary>
        static double ParseExp(string jwt)
        {
            if (string.IsNullOrEmpty(jwt)) return 0;
            try
            {
                var parts = jwt.Split('.');
                if (parts.Length < 2) return 0;
                var payload = parts[1].Replace('-', '+').Replace('_', '/');
                switch (payload.Length % 4)
                {
                    case 2: payload += "=="; break;
                    case 3: payload += "="; break;
                    case 1: return 0;
                }
                var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));
                var claims = JsonUtility.FromJson<JwtClaims>(json);
                return claims != null ? claims.exp : 0;
            }
            catch { return 0; }
        }

        [Serializable] private class JwtClaims { public double exp; }
        [Serializable] private class SessionEnvelope { public SessionData data; }
        [Serializable] private class SessionData { public string token; }

        // ── Reads ────────────────────────────────────────────────────────────

        public Coroutine GetConfig(Action<GameConfigDto> onOk, Action<string> onErr = null) =>
            StartCoroutine(Get($"{Base}/games/{gameKey}/config", raw =>
            {
                var env = JsonUtility.FromJson<ConfigEnvelope>(raw);
                onOk?.Invoke(env != null && env.data != null ? env.data.config : null);
            }, onErr));

        public Coroutine GetCurrentRound(Action<GameRoundDto> onOk, Action<string> onErr = null) =>
            StartCoroutine(Get($"{Base}/games/{gameKey}/current", raw =>
            {
                var env = JsonUtility.FromJson<RoundEnvelope>(raw);
                var round = env != null && env.data != null ? env.data.round : null;
                if (round != null)
                {
                    round.betsByItem = JsonMap.ExtractLongMap(raw, "betsByItem");
                    // Sync countdowns to the SERVER clock (meta.timestamp), not
                    // the device clock — kills the per-device 1-2s drift.
                    if (env.meta != null && !string.IsNullOrEmpty(env.meta.timestamp))
                        round.SetServerNow(env.meta.timestamp);
                }
                onOk?.Invoke(round);
            }, onErr));

        public Coroutine GetBalance(Action<BalanceDto> onOk, Action<string> onErr = null) =>
            StartCoroutine(Get($"{Base}/games/{gameKey}/balance", raw =>
            {
                var env = JsonUtility.FromJson<BalanceEnvelope>(raw);
                onOk?.Invoke(env != null && env.data != null ? env.data.balance : null);
            }, onErr));

        // All-day top players (for the on-demand leaderboard button).
        public Coroutine GetLeaderboard(Action<LeaderboardEntryDto[]> onOk, Action<string> onErr = null) =>
            StartCoroutine(Get($"{Base}/games/{gameKey}/leaderboard/today", raw =>
            {
                var env = JsonUtility.FromJson<LeaderboardEnvelope>(raw);
                onOk?.Invoke(env != null && env.data != null ? env.data.entries : null);
            }, onErr));

        // Yesterday's top players (for the yesterday leaderboard tab).
        public Coroutine GetLeaderboardYesterday(Action<LeaderboardEntryDto[]> onOk, Action<string> onErr = null) =>
            StartCoroutine(Get($"{Base}/games/{gameKey}/leaderboard/yesterday", raw =>
            {
                var env = JsonUtility.FromJson<LeaderboardEnvelope>(raw);
                onOk?.Invoke(env != null && env.data != null ? env.data.entries : null);
            }, onErr));

        // Retrieve the current live jackpot pool value from the server.
        public Coroutine GetJackpot(Action<JackpotDto> onOk, Action<string> onErr = null) =>
            StartCoroutine(Get($"{Base}/games/{gameKey}/jackpot", raw =>
            {
                var env = JsonUtility.FromJson<JackpotEnvelope>(raw);
                onOk?.Invoke(env != null && env.data != null ? env.data.jackpot : null);
            }, onErr));

        // Winners of THIS round (for the post-round result panel).
        public Coroutine GetRoundLeaderboard(Action<LeaderboardEntryDto[]> onOk, Action<string> onErr = null) =>
            StartCoroutine(Get($"{Base}/games/{gameKey}/leaderboard/round", raw =>
            {
                var env = JsonUtility.FromJson<LeaderboardEnvelope>(raw);
                onOk?.Invoke(env != null && env.data != null ? env.data.entries : null);
            }, onErr));

        // Retrieve personal game records (which rounds played, selections, payouts, balances)
        public Coroutine GetGameRecords(Action<GameRecordDto[]> onOk, Action<string> onErr = null) =>
            StartCoroutine(Get($"{Base}/games/{gameKey}/records", raw =>
            {
                var env = JsonUtility.FromJson<RecordsEnvelope>(raw);
                onOk?.Invoke(env != null && env.data != null ? env.data.records : null);
            }, onErr));

        // Get past rounds winning item history
        public Coroutine GetHistory(Action<string[]> onOk, Action<string> onErr = null) =>
            StartCoroutine(Get($"{Base}/games/{gameKey}/history", raw =>
            {
                var env = JsonUtility.FromJson<HistoryEnvelope>(raw);
                onOk?.Invoke(env != null && env.data != null ? env.data.history : null);
            }, onErr));

        // ── Bet placement ────────────────────────────────────────────────────

        // ── Item registration ────────────────────────────────────────────────

        /// <summary>
        /// Pushes Unity's authoritative button layout (buttonID, category, multiplier)
        /// to the dev server so pickWinners() and settleRound() use the same mapping
        /// as BetButtonData ScriptableObjects in the Editor. Call once on startup.
        /// </summary>
        public Coroutine RegisterItems(RegisterItemDto[] items, Action onOk = null, Action<string> onErr = null)
        {
            var body = JsonUtility.ToJson(new RegisterItemsBody { items = items });
            return StartCoroutine(Post($"{Base}/games/{gameKey}/register-items", body, _ => onOk?.Invoke(), onErr));
        }

        public Coroutine PostBet(string item, long amount, Action<GameRoundDto> onOk, Action<string> onErr = null)
        {
            var body = JsonUtility.ToJson(new BetBody { item = item, amount = amount });
            return StartCoroutine(Post($"{Base}/games/{gameKey}/bet", body, raw =>
            {
                var env = JsonUtility.FromJson<BetEnvelope>(raw);
                var round = env != null && env.data != null ? env.data.round : null;
                if (round != null)
                    round.betsByItem = JsonMap.ExtractLongMap(raw, "betsByItem");
                onOk?.Invoke(round);
            }, onErr));
        }

        [Serializable]
        private class BetBody { public string item; public long amount; }

        // ── Add coins (top-up) ───────────────────────────────────────────────
        [Tooltip("Backend path for the add-coins/top-up request (e.g. /games/{gameKey}/add-coins). " +
                 "Left blank until the endpoint is decided — the Add button logs a warning instead of calling out.")]
        public string addCoinsPath = "";

        public Coroutine PostAddCoins(Action<string> onOk = null, Action<string> onErr = null)
        {
            if (string.IsNullOrEmpty(addCoinsPath))
            {
                Debug.LogWarning("[ApiClient] addCoinsPath is not set yet — Add button has no endpoint to call.");
                onErr?.Invoke("Add-coins endpoint not configured");
                return null;
            }
            return StartCoroutine(Post($"{Base}{addCoinsPath}", "{}", onOk, onErr));
        }

        // ── Recharge (top-up via external/payment flow) ─────────────────────────
        [Tooltip("Backend path for the recharge request (e.g. /games/{gameKey}/recharge). " +
                 "Left blank until the endpoint is decided — the Recharge button logs a warning instead of calling out.")]
        public string rechargePath = "";

        public Coroutine PostRecharge(Action<string> onOk = null, Action<string> onErr = null)
        {
            if (string.IsNullOrEmpty(rechargePath))
            {
                Debug.LogWarning("[ApiClient] rechargePath is not set yet — Recharge button has no endpoint to call.");
                onErr?.Invoke("Recharge endpoint not configured");
                return null;
            }
            return StartCoroutine(Post($"{Base}{rechargePath}", "{}", onOk, onErr));
        }

        // ── HTTP plumbing ──────────────────────────────────────────────────────

        IEnumerator Get(string url, Action<string> onOk, Action<string> onErr)
        {
            for (int attempt = 0; ; attempt++)
            {
                using var req = UnityWebRequest.Get(url);
                ApplyHeaders(req);
                yield return req.SendWebRequest();

                if (ShouldRetryAfterRefresh(req, attempt))
                {
                    bool refreshed = false;
                    yield return Refresh(ok => refreshed = ok);
                    if (refreshed) continue;
                }
                Handle(req, onOk, onErr);
                yield break;
            }
        }

        IEnumerator Post(string url, string json, Action<string> onOk, Action<string> onErr)
        {
            for (int attempt = 0; ; attempt++)
            {
                using var req = new UnityWebRequest(url, "POST");
                byte[] payload = System.Text.Encoding.UTF8.GetBytes(json);
                req.uploadHandler = new UploadHandlerRaw(payload);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                ApplyHeaders(req);
                yield return req.SendWebRequest();

                // A 401 means the request was rejected before it did anything, so
                // replaying it after a refresh cannot double-place a bet.
                if (ShouldRetryAfterRefresh(req, attempt))
                {
                    bool refreshed = false;
                    yield return Refresh(ok => refreshed = ok);
                    if (refreshed) continue;
                }
                Handle(req, onOk, onErr);
                yield break;
            }
        }

        /// <summary>Retry once, and only once, on an expired-token 401.</summary>
        bool ShouldRetryAfterRefresh(UnityWebRequest req, int attempt) =>
            attempt == 0 &&
            req.responseCode == 401 &&
            !SessionEnded &&
            !string.IsNullOrEmpty(Token);

        void ApplyHeaders(UnityWebRequest req)
        {
            if (!string.IsNullOrEmpty(Token))
                req.SetRequestHeader("Authorization", "Bearer " + Token);
            // Defeat WebView/CDN caching of the fast-changing /current endpoint.
            req.SetRequestHeader("Cache-Control", "no-cache");
        }

        void Handle(UnityWebRequest req, Action<string> onOk, Action<string> onErr)
        {
            var raw = req.downloadHandler != null ? req.downloadHandler.text : string.Empty;
            if (req.result == UnityWebRequest.Result.Success)
            {
                onOk?.Invoke(raw);
                return;
            }
            // Try to surface the backend error envelope's message/code.
            string message = req.error ?? "Request failed";
            try
            {
                var err = JsonUtility.FromJson<ErrorEnvelope>(raw);
                if (err != null && err.error != null && !string.IsNullOrEmpty(err.error.message))
                    message = err.error.message;
            }
            catch { /* non-JSON error body */ }
            Debug.LogWarning($"[ApiClient] {req.url} -> {message}");
            onErr?.Invoke(message);
        }
    }
}
