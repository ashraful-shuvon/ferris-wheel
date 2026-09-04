using System.Runtime.InteropServices;
using UnityEngine;

namespace Zimo.Net
{
    /// <summary>
    /// Reads launch params (token / api base) passed by the Flutter WebView host
    /// on the WebGL page URL, and forwards a "close" request back to the host.
    ///
    /// On WebGL the values come from the ZimoBridge.jslib plugin. In the Editor
    /// (or a desktop build) the jslib isn't available, so it falls back to
    /// inspector-set dev defaults so the game can be tested locally against a
    /// dev backend.
    /// </summary>
    public static class WebBridge
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern string ZimoGetUrlParam(string name);

        [DllImport("__Internal")]
        private static extern void ZimoRequestClose();

        [DllImport("__Internal")]
        private static extern void ZimoPostMessage(string json);
#endif

        /// <summary>Editor / non-WebGL dev fallbacks. Set these in a bootstrap
        /// object or leave blank to run the offline demo.</summary>
        public static string EditorApiBase = "http://127.0.0.1:5002/api/v1";
        public static string EditorToken = "";

        public static string GetParam(string name)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try { return ZimoGetUrlParam(name) ?? string.Empty; }
            catch { return string.Empty; }
#else
            return string.Empty;
#endif
        }

        /// <summary>API base, e.g. https://games.zimolive.com/api/v1.</summary>
        public static string ApiBase
        {
            get
            {
                var p = GetParam("api");
                return string.IsNullOrEmpty(p) ? EditorApiBase : p;
            }
        }

        /// <summary>Fresh token minted by POST /games/session/extend. Once set it
        /// wins over the launch URL param, which is a one-shot 15-minute token.</summary>
        private static string _extendedToken;

        /// <summary>Player JWT (empty when running the offline demo).</summary>
        public static string Token
        {
            get
            {
                if (!string.IsNullOrEmpty(_extendedToken)) return _extendedToken;
                var p = GetParam("token");
                return string.IsNullOrEmpty(p) ? EditorToken : p;
            }
        }

        /// <summary>Install the token returned by a session extension.</summary>
        public static void SetToken(string token)
        {
            _extendedToken = string.IsNullOrEmpty(token) ? null : token.Trim();
        }

        /// <summary>True when launched embedded with a real session token.</summary>
        public static bool HasSession => !string.IsNullOrEmpty(Token);

        /// <summary>Ask the Flutter host to close the WebView.</summary>
        /// <summary>
        /// Post a `{ "type": ... }` message to the Flutter host.
        ///
        /// The host decides what each type means, so an in-game button can drive
        /// app-level navigation (the wallet, a share sheet) without the game
        /// knowing anything about the app. Unknown types are ignored by the
        /// host, so a game can post one before the app understands it.
        /// </summary>
        public static void Post(string type)
        {
            if (string.IsNullOrEmpty(type)) return;
#if UNITY_WEBGL && !UNITY_EDITOR
            try { ZimoPostMessage("{\"type\":\"" + type + "\"}"); } catch { /* not embedded */ }
#else
            Debug.Log($"[WebBridge] Post({type}) (no-op outside WebGL).");
#endif
        }

        /// <summary>Ask the Flutter host to open its wallet / recharge screen.</summary>
        public static void RequestRecharge() => Post("recharge");

        public static void RequestClose()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try { ZimoRequestClose(); } catch { /* not embedded */ }
#else
            Debug.Log("[WebBridge] RequestClose (no-op outside WebGL).");
#endif
        }
    }
}
