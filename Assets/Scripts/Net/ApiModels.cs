using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zimo.Net
{
    // DTOs mirror the games backend JSON. JsonUtility handles fixed-name nested
    // objects + arrays; the dynamic `betsByItem` map is parsed separately via
    // JsonMap (JsonUtility cannot deserialize arbitrary-key maps).

    [Serializable]
    public class GameItemDto
    {
        public string key;
        public string label;
        public float multiplier;
        public string category; // "Pizza" | "Vegetable" — must match BetButtonData.category
    }

    [Serializable]
    public class GameConfigDto
    {
        public string gameKey;
        public string title;
        public string currency;
        public float rtpPercent;
        public GameItemDto[] items;
        public long[] betTiers;
        public int bettingMs;
        public int spinMs;
        public int intermissionMs;

        // warmupMs — duration of the "warmup" phase = "Show Time": the RoundResultPanel
        // + leaderboard window that follows the spin. The next round's betting
        // opens right after (3-phase loop: Bet Time → Drawing → Show Time). If the
        // server omits it the field is 0 and GameManager uses its Inspector value.
        public int warmupMs;

        // spinIntervalStartMs / spinIntervalEndMs — tick interval (ms) at the
        // start vs. end of the chase animation, matching GameManager's
        // spinIntervalStart / spinIntervalEnd (which are in seconds). If the
        // server does not send these they deserialise as 0 and GameManager
        // keeps its Inspector values, same fallback as warmupMs.
        public int spinIntervalStartMs;
        public int spinIntervalEndMs;

        public string spinCurveType; // "linear" | "easeIn" | "easeOut" | "easeInOut"
        public string winMode; // "single" | "combo" — server decides, client just renders payouts accordingly
    }

    [Serializable]
    public class GameRoundDto
    {
        public string id;
        public string gameKey;
        public int roundNumber;

        // Internal phases: betting | closed | spinning | result | warmup | completed
        // The player sees a 3-phase loop: Bet Time (betting) → Drawing (spinning)
        // → Show Time (warmup / RoundResultPanel window) → back to Bet Time.
        public string phase;

        public string startedAt;      // ISO 8601
        public string bettingClosesAt;
        public string spinEndsAt;

        // Set by the server when the round enters "warmup" (Show Time) so the
        // client can drive a live countdown to the panel-close from the real
        // server clock rather than a guessed local timer.
        public string warmupEndsAt;   // ISO 8601 — when Show Time (warmup) ends

        public string currency;
        public GameItemDto[] items;
        public float rtpPercent;
        public int betCount;
        public long totalBet;
        public string winningItem;    // null until spinning — single representative, used for the spin-to animation
        public string[] winningItems; // null until spinning — ALL item keys that pay out this round.
                                       // single mode: one entry. combo mode: every item in the chosen category.
        public string winMode;        // "single" | "combo" — echoed live every poll, since the admin can change
        public string winCategory;    // "Pizza" | "Vegetable" — it mid-session and a one-shot /config fetch would go stale
                                       // Server computes this from (winCategory, winMode) — client never derives it.
        public long totalPayout;

        // Key of the item currently marked "hot" for this round (e.g. "B2"), or
        // null/empty when no item is hot this round. Rolled fresh by the server
        // each time a new round starts.
        public string hotItem;

        // Filled by ApiClient from the raw JSON (JsonUtility skips the map).
        [NonSerialized] public Dictionary<string, long> betsByItem = new Dictionary<string, long>();

        // Server clock at the moment this response was generated (from the
        // envelope's `meta.timestamp`). Used to sync the client's countdowns to
        // the SERVER clock instead of the device clock (which is often skewed a
        // second or two and differs per device). Filled by ApiClient.
        [NonSerialized] public DateTime serverNowUtc = DateTime.UtcNow;
        public void SetServerNow(string iso) => serverNowUtc = ParseIso(iso);

        public DateTime BettingClosesAtUtc  => ParseIso(bettingClosesAt);
        public DateTime SpinEndsAtUtc       => ParseIso(spinEndsAt);
        public DateTime WarmupEndsAtUtc     => ParseIso(warmupEndsAt);

        private static DateTime ParseIso(string s)
        {
            if (string.IsNullOrEmpty(s)) return DateTime.UtcNow;
            return DateTime.TryParse(
                s,
                null,
                System.Globalization.DateTimeStyles.AdjustToUniversal |
                    System.Globalization.DateTimeStyles.AssumeUniversal,
                out var dt)
                ? dt.ToUniversalTime()
                : DateTime.UtcNow;
        }
    }

    [Serializable]
    public class BalanceDto
    {
        public string username;
        public long coins;
        public long diamonds;
        public bool frozen;
        public long todaysWin; // player's authoritative today's-win total, server-calculated
        public string avatar;  // player's real avatar URL (blank for sandbox testers)
    }

    [Serializable]
    public class LeaderboardEntryDto
    {
        public int    rank;
        public string username;
        public long   wonAmount;
        public long   todaysWin;
        public string avatar;
    }

    [Serializable]
    public class LeaderboardListDto
    {
        public LeaderboardEntryDto[] entries;
    }

    [Serializable]
    public class BetResultDto
    {
        public GameRoundDto round;
    }

    // ── Envelope wrappers (backend wraps every response in { success, data }) ──
    [Serializable] public class ConfigEnvelope { public bool success; public ConfigData data; }
    [Serializable] public class ConfigData { public GameConfigDto config; }

    [Serializable] public class RoundEnvelope { public bool success; public RoundData data; public MetaDto meta; }
    [Serializable] public class RoundData { public GameRoundDto round; }
    [Serializable] public class MetaDto { public string timestamp; }

    [Serializable] public class BalanceEnvelope { public bool success; public BalanceData data; }
    [Serializable] public class BalanceData { public BalanceDto balance; }

    [Serializable] public class LeaderboardEnvelope { public bool success; public LeaderboardListDto data; }

    [Serializable] public class BetEnvelope { public bool success; public BetResultDto data; }

    [Serializable] public class HistoryEnvelope { public bool success; public HistoryData data; }
    [Serializable] public class HistoryData { public string[] history; }

    [Serializable] public class ErrorEnvelope { public bool success; public ApiErrorDto error; }
    [Serializable] public class ApiErrorDto { public string code; public string message; }

    // Sent once on startup to register Unity's authoritative button layout.
    [Serializable]
    public class RegisterItemDto
    {
        public string key;
        public string category;
        public float  multiplier;
        public string label;
    }

    [Serializable]
    public class RegisterItemsBody
    {
        public RegisterItemDto[] items;
    }

    /// <summary>
    /// Minimal extractor for the dynamic `"betsByItem": { "B1": 100, ... }` map.
    /// Avoids pulling in a full JSON library just for one variable-key object.
    /// </summary>
    public static class JsonMap
    {
        public static Dictionary<string, long> ExtractLongMap(string json, string field)
        {
            var result = new Dictionary<string, long>();
            if (string.IsNullOrEmpty(json)) return result;
            var needle = "\"" + field + "\"";
            int idx = json.IndexOf(needle, StringComparison.Ordinal);
            if (idx < 0) return result;
            int brace = json.IndexOf('{', idx);
            if (brace < 0) return result;
            int end = json.IndexOf('}', brace);
            if (end < 0) return result;
            var body = json.Substring(brace + 1, end - brace - 1);
            if (string.IsNullOrWhiteSpace(body)) return result;
            foreach (var pair in body.Split(','))
            {
                var kv = pair.Split(':');
                if (kv.Length != 2) continue;
                var k = kv[0].Trim().Trim('"');
                if (long.TryParse(kv[1].Trim(), out var v) && !string.IsNullOrEmpty(k))
                    result[k] = v;
            }
            return result;
        }
    }

    [Serializable]
    public class BetSelectionDto
    {
        public string key;
        public long amount;
    }

    [Serializable]
    public class GameRecordDto
    {
        public int roundNumber;
        public string timestamp;
        public BetSelectionDto[] selectedFood;
        public string[] winningFruits;
        public long winCoins;
        public long balanceBefore;
        public long balanceAfter;
    }

    [Serializable]
    public class RecordsEnvelope
    {
        public bool success;
        public RecordsData data;
    }

    [Serializable]
    public class RecordsData
    {
        public GameRecordDto[] records;
    }

    [Serializable]
    public class JackpotDto
    {
        public long jackpot;
        public string endsAt;
    }

    [Serializable]
    public class JackpotData
    {
        public JackpotDto jackpot;
    }

    [Serializable]
    public class JackpotEnvelope
    {
        public bool success;
        public JackpotData data;
    }
}