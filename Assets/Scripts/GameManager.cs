using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using TMPro;
using Zimo.Net;

public enum SpinCurveType { Linear, EaseIn, EaseOut, EaseInOut }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Leaderboard")]
    public LeaderboardPanel leaderboardPanel;

    [Header("Ranking Jackpot")]
    public RankingJackpotPanel rankingJackpotPanel;
    public Button rankingJackpotButton;

    [Header("Audio")]
    public GameAudioManager audioManager;

    [Header("Round History")]
    public RoundHistoryTracker historyTracker;
    
    [Header("Today's Win")]
    public TodaysWinTracker todaysWinTracker;

    [Header("Bet Buttons  (B1 - B8, in order)")]
    public List<BetButton> betButtons = new List<BetButton>();

    [Header("Bet Amount Buttons")]
    public List<CoinButton> amountButtons = new List<CoinButton>();

    [Header("Jar Effect Setup")]
    public Transform jarGameObject; // Drag your Jar GameObject or RectTransform here in the inspector
    public FakeBetCoinShower fakeBetCoinShower; // Ensure your fake shower reference is hooked up
    
    [Header("UI")]
    public TextMeshProUGUI coinBalanceText;
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI statusText;
    public Text            StatusText;
    public TextMeshProUGUI usernameText;
    public Button          repeatButton;

    [Tooltip("Shows the current server round number, e.g. \"Today: Round 42\". " +
             "Assign the 'CurrentRound' object's text component here.")]
    public TextMeshProUGUI currentRoundText;

    [Tooltip("Label shown only while the wheel is spinning (Drawing phase), e.g. \"Spinning\" " +
             "with animated dots. Its GameObject is hidden during Bet Time and Show Time. " +
             "Position it anywhere you like — the script only writes its text and visibility.")]
    public TextMeshProUGUI spinningText;

    [Tooltip("Base wording for spinningText. Animated dots are appended to it.")]
    public string spinningLabel = "Spinning";

    [Tooltip("How many dots cycle after the label (1 → \".\", 3 → \"...\").")]
    [Range(1, 5)]
    public int spinningDotCount = 3;

    [Tooltip("Seconds between dot steps.")]
    public float spinningDotInterval = 0.35f;

    [Header("Timing")]
    [Tooltip("Length of the betting window. Overwritten by the server's bettingMs " +
             "(GameConfigDto) on startup when 'Use Server Timing' is on and we're in " +
             "server mode — this Inspector value is only the fallback/offline default.")]
    public float bettingDuration   = 30f;

    [Tooltip("Total time the chase animation is allowed to take. Overwritten by the " +
             "server's spinMs (GameConfigDto) the same way as bettingDuration. Note the " +
             "chase can still run a little past this on purpose (see SpinToWinner) so a " +
             "2nd lap fits — this value is a target, not a hard cutoff.")]
    public float spinDuration      = 4f;

    [Tooltip("Tick interval at the START of the chase (FAST — small value). The wheel " +
             "opens fast and decelerates from here. Not sent by the server (it has no " +
             "concept of visual chase speed) — tune this in the Inspector, or change it " +
             "at runtime, e.g. from a debug menu.")]
    public float spinIntervalStart = 0.07f;

    [Tooltip("Tick interval at the END of the chase, just before landing (SLOW — large " +
             "value). The wheel slows down to this as it lands. Not sent by the server — " +
             "same notes as spinIntervalStart.")]
    public float spinIntervalEnd   = 0.35f;

    [Tooltip("The deceleration curve of the spin.")]
    public SpinCurveType spinCurve = SpinCurveType.EaseInOut;

    [Tooltip("How long the winning button(s) stay glowing — fully landed, no longer " +
             "chasing — before the RoundResultPanel is shown. Overwritten by the server's " +
             "intermissionMs (GameConfigDto). Keep this short (1-2 s) now that warmup " +
             "owns the panel window.")]
    public float winGlowHoldDuration = 2f;

    [Tooltip("Duration of the 'warmup' phase: RoundResultPanel is shown during this window. " +
             "Overwritten by the server's warmupMs. If the server does not yet send " +
             "warmupMs this Inspector value is used as the fallback.")]
    public float warmupDuration = 5f;

    [Tooltip("If true and we're in server mode, bettingDuration/spinDuration/" +
             "winGlowHoldDuration/warmupDuration are overwritten from the " +
             "server's /config response once at startup. " +
             "Turn this off to force all Inspector values even in server mode.")]
    public bool useServerTiming = true;

    [Header("Betting Start Banner")]
    public BettingStartBanner bettingStartBanner;

    [Header("Win / Lose Panel")]
    [Tooltip("Shown during the warmup phase when the player placed a bet. " +
             "Separate from the leaderboard panel.")]
    [FormerlySerializedAs("winLosePanel")]
    public RoundResultPanel roundResultPanel;

    [Tooltip("Minimum hold duration (in seconds) for the Win/Lose panel.")]
    public float minWinLoseHoldDuration = 2.0f;

    [Header("Game Records")]
    public GameRecordsPanel gameRecordsPanel;
    public Button gameRecordsButton;

    [Header("Combo Sprites")]
    public Sprite comboSaladSprite;
    public Sprite comboPizzaSprite;

    [Header("Food Sprites  (B1 - B8, in order)")]
    public List<Sprite> foodSprites = new List<Sprite>();

    [Header("Coin Fly Effect")]
    public CoinFlyEffect coinFlyEffect;

    [Header("Bear Animator")]
    public BearAnimator bearAnimator;

    [Header("Backend")]
    public ApiClient api;
    public float serverPollInterval = 1.5f;

    // ── Private state ─────────────────────────────────────────────────────────

    long   playerCoins;
    long   selectedBetAmount  = 0;
    int    selectedAmountIndex = -1;
    long   lastBetAmount      = 0;

    // Server-controlled win mode — refreshed from every round poll so an
    // admin changing it mid-session is reflected without a reconnect.
    string serverWinMode = "single";

    enum Phase { Idle, Betting, Spinning, Result, Warmup }
    Phase _phase = Phase.Idle;
    Phase phase
    {
        get => _phase;
        set
        {
            _phase = value;
            UpdateMatchStatusText();
        }
    }

    public bool IsBetting => serverBettingOpen;

    Coroutine watchdogRoutine;

    // ── Last Result Cache ──
    bool lastRoundPlayed = false;
    bool lastRoundWon = false;
    int  lastRoundWinnerIdx = -1;
    bool lastRoundCombo = false;
    long lastRoundWonAmount = 0;
    bool lastRoundParticipated = false;
    long lastRoundBetAmount = 0;
    int  lastRoundNumber = 0;
    List<LeaderboardEntry> lastRoundEntries = null;

    // ── Server mode state ─────────────────────────────────────────────────────

    int    serverRoundNumber  = -1;
    int    serverSpunRound    = -1;
    int    serverLitRound     = -1;
    int    serverPoppedRound  = -1;
    int    serverWarmupRound  = -1;   // tracks which round's warmup we've started
    bool   serverBettingOpen;
    System.DateTime serverBettingEndsUtc;
    bool   serverSpinning;            // true during the "spinning"/Drawing phase
    System.DateTime serverSpinEndsUtc; // when the spin/Drawing window ends (from the DB round)
    bool   serverShowTime;            // true during "warmup" — the win/lose panel window
    System.DateTime serverShowEndsUtc; // when Show Time (the panel window) ends (warmupEndsAt)

    // Server-clock sync: offset (seconds) between the SERVER clock (meta.timestamp)
    // and this device's clock, so every countdown runs on the server clock instead
    // of the device clock (often skewed a second or two, and it differs per device
    // — that's the phase-transition glitch). Smoothed with an EMA to absorb the
    // per-poll network-latency jitter.
    double clockOffsetSec;
    bool   clockSynced;

    /// <summary>Now, on the SERVER clock (device clock + measured offset). Use this
    /// for every countdown so all devices agree on the phase timing.</summary>
    System.DateTime ServerUtcNow => System.DateTime.UtcNow.AddSeconds(clockOffsetSec);

    void SyncServerClock(GameRoundDto r)
    {
        if (r == null) return;
        double sample = (r.serverNowUtc - System.DateTime.UtcNow).TotalSeconds;
        if (System.Math.Abs(sample) > 24 * 3600) return; // ignore an unparsed/garbage stamp
        if (!clockSynced)
            clockOffsetSec = sample;
        else if (System.Math.Abs(sample - clockOffsetSec) > 3.0)
            clockOffsetSec = sample;                              // big jump → snap
        else
            clockOffsetSec += (sample - clockOffsetSec) * 0.3;   // EMA smooth
        clockSynced = true;
    }

    // Snapshot of myBetsByItem total taken at the START of ResolveServerResult,
    // before any yield point where ResetForServerRound() could clear the dict.
    // Used instead of live TotalMyBets() for the RoundResultPanel show check.
    long   resolveRoundBetSnapshot = 0;

    readonly Dictionary<string, long> myBetsByItem = new Dictionary<string, long>();

    // Latest pooled bets across ALL players (from the server's round.betsByItem).
    // Used to show OTHER players' bets in realtime (coin shower) — others' total
    // for an item is pooled minus my own.
    Dictionary<string, long> lastBetsByItem = new Dictionary<string, long>();

    // Optimistic pooled bet, so YOUR stake lands in the pooled "TOTAL" badge on
    // the frame you tap it instead of one poll + two round trips later.
    //
    // Only this device sees it early. Everyone else picks your bet up on their
    // own next poll, which is correct and unchanged — this is a display
    // shortcut, never a source of truth, and it never reaches the server.
    //
    // Why a MAX against the server value rather than an addition: within a
    // round the pool only ever grows, so max() can never double-count your
    // stake once the server echoes it back, and can never let the badge tick
    // backwards while your bet is still in flight. An additive overlay does
    // both — it double-counts for one tick if a poll happens to land between
    // the server committing your bet and its reply reaching us.
    readonly Dictionary<string, long> optimisticBetsByItem = new Dictionary<string, long>();

    /// <summary>The current server round number (server mode).</summary>
    public int ServerRoundNumber => serverRoundNumber;
    /// <summary>
    /// Pooled bet (all players) for an item key: the last poll's server value,
    /// or your own optimistic total if the server hasn't echoed your bet yet.
    /// </summary>
    public long PooledBetForKey(string key)
    {
        if (key == null) return 0;
        long server     = lastBetsByItem != null && lastBetsByItem.TryGetValue(key, out var v) ? v : 0;
        long optimistic = optimisticBetsByItem.TryGetValue(key, out var o) ? o : 0;
        return server > optimistic ? server : optimistic;
    }

    /// <summary>The server's own pooled figure, with no optimistic overlay.</summary>
    public long ServerPooledBetForKey(string key) =>
        key != null && lastBetsByItem != null && lastBetsByItem.TryGetValue(key, out var v) ? v : 0;
    /// <summary>This player's own bet for an item key this round.</summary>
    public long MyBetForKey(string key) =>
        key != null && myBetsByItem.TryGetValue(key, out var v) ? v : 0;

    // Handle to the currently-running SpinToWinner coroutine (server mode only).
    Coroutine spinToWinnerRoutine;

    // Handle to the currently-running ResolveServerResult coroutine.
    Coroutine resolveResultRoutine;

    // ── Unity Lifecycle ───────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (roundResultPanel != null)
        {
            roundResultPanel.holdDuration = Mathf.Max(minWinLoseHoldDuration, roundResultPanel.holdDuration);
        }

        if (gameRecordsButton != null)
        {
            gameRecordsButton.onClick.AddListener(OpenGameRecords);
        }

        if (rankingJackpotButton != null)
        {
            rankingJackpotButton.onClick.AddListener(OnViewRankingJackpotClicked);
        }

        foreach (var btn in betButtons)
        {
            btn.gameObject.SetActive(true);
            btn.ResetVisuals();
            btn.SetBrightOverlayActive(false);
        }

        UpdateCoinUI();
        SetStatus("Bet Time");
        if (spinningText != null) spinningText.gameObject.SetActive(false);
        SetRepeatInteractable(false);

        for (int i = 0; i < amountButtons.Count; i++)
        {
            int idx = i;
            var btn = amountButtons[i].GetComponent<Button>();
            if (btn != null) btn.onClick.AddListener(() => SelectAmount(idx));
        }

        for (int i = 0; i < betButtons.Count; i++)
        {
            int idx = i;
            betButtons[i].GetComponent<Button>().onClick.AddListener(() => OnBetButtonClicked(idx));
        }

        if (repeatButton != null)
            repeatButton.onClick.AddListener(OnRepeatClicked);

        watchdogRoutine = StartCoroutine(ButtonActiveWatchdog());

        SetStatus("Connecting...");
        SetBetButtonsInteractable(false);
        StartCoroutine(ServerStartupSequence());
    }

    IEnumerator ServerStartupSequence()
    {
        RegisterItemsWithServer();
        api.GetBalance(ApplyBalance);

        bool configDone = false;
        api.GetConfig(cfg => { ApplyServerConfig(cfg); configDone = true; },
                       _   => { configDone = true; }); 

        if (historyTracker != null)
        {
            RefreshHistoryFromServer();
        }

        float t = 0f;
        while (!configDone && t < 5f) { t += Time.deltaTime; yield return null; }

        StartCoroutine(ServerRoundLoop());
    }

    void Update()
    {
        UpdateSpinningLabel();

        if (timerText == null) return;

        // Live countdown driven by the DB round's timestamps:
        //   Bet Time → counts down to bettingClosesAt
        //   Drawing  → counts down to spinEndsAt (was frozen at 0 before)
        if (serverBettingOpen)
        {
            double remaining = (serverBettingEndsUtc - ServerUtcNow).TotalSeconds;
            timerText.text = Mathf.Max(0, Mathf.CeilToInt((float)remaining)).ToString();
        }
        else if (serverSpinning)
        {
            double remaining = (serverSpinEndsUtc - ServerUtcNow).TotalSeconds;
            timerText.text = Mathf.Max(0, Mathf.CeilToInt((float)remaining)).ToString();
        }
        else if (serverShowTime)
        {
            // Show Time = the win/lose panel window: tick the countdown down while
            // the panel is up; it closes when this hits 0.
            double remaining = (serverShowEndsUtc - ServerUtcNow).TotalSeconds;
            timerText.text = Mathf.Max(0, Mathf.CeilToInt((float)remaining)).ToString();
        }
    }

    /// <summary>
    /// Shows "Spinning" + cycling dots while the wheel is drawing, hides it otherwise.
    /// Driven off serverSpinning so it matches the server phase, same as timerText.
    /// </summary>
    void UpdateSpinningLabel()
    {
        if (spinningText == null) return;

        bool spinning = serverSpinning || phase == Phase.Spinning;

        if (spinningText.gameObject.activeSelf != spinning)
            spinningText.gameObject.SetActive(spinning);

        if (!spinning) return;

        int steps = Mathf.Max(1, spinningDotCount);
        float interval = (spinningDotInterval > 0f) ? spinningDotInterval : 0.35f;
        int dots = 1 + (Mathf.FloorToInt(Time.time / interval) % steps);

        spinningText.text = spinningLabel + new string('.', dots);
    }

    public void CloseGame() => WebBridge.RequestClose();

    // ── Balance ───────────────────────────────────────────────────────────────

    /// <summary>The logged-in player's real avatar URL (from the server balance).
    /// Blank for sandbox testers — UI should fall back to a default sprite.</summary>
    public string PlayerAvatarUrl { get; private set; } = "";

    void ApplyBalance(BalanceDto b)
    {
        if (b == null) return;
        playerCoins = b.coins;
        UpdateCoinUI();
        if (todaysWinTracker != null) todaysWinTracker.SetServerValue(b.todaysWin);
        if (usernameText != null && !string.IsNullOrEmpty(b.username)) usernameText.text = b.username;
        if (!string.IsNullOrEmpty(b.avatar)) PlayerAvatarUrl = b.avatar;
    }

    // ── Server Config / Dynamic Timing ────────────────────────────────────────

    void ApplyServerConfig(GameConfigDto cfg)
    {
        serverWinMode = (cfg != null && !string.IsNullOrEmpty(cfg.winMode)) ? cfg.winMode : "single";

        if (cfg == null || !useServerTiming) return;

        if (cfg.bettingMs    > 0) bettingDuration    = cfg.bettingMs    / 1000f;
        if (cfg.spinMs       > 0) spinDuration        = cfg.spinMs       / 1000f;
        if (cfg.intermissionMs > 0) winGlowHoldDuration = cfg.intermissionMs / 1000f;
        if (cfg.warmupMs     > 0) warmupDuration      = cfg.warmupMs     / 1000f;

        // Visual chase speed — the wheel opens fast (start) and eases to slow
        // (end). These are the real controls for the spin feel; spinMs above
        // only governs how long the server keeps the round in the spinning phase.
        if (cfg.spinIntervalStartMs > 0) spinIntervalStart = cfg.spinIntervalStartMs / 1000f;
        if (cfg.spinIntervalEndMs   > 0) spinIntervalEnd   = cfg.spinIntervalEndMs   / 1000f;
        
        if (!string.IsNullOrEmpty(cfg.spinCurveType))
        {
            if (System.Enum.TryParse<SpinCurveType>(cfg.spinCurveType, true, out var parsedCurve))
            {
                spinCurve = parsedCurve;
            }
        }

        // Panel visible time is owned by the "Show Time" (warmup) window: fit
        // hold = warmup − open/close so the panel opens then closes right as
        // Show Time ends and the next Bet Time opens.
        if (roundResultPanel != null)
            roundResultPanel.holdDuration = Mathf.Max(
                minWinLoseHoldDuration,
                warmupDuration - roundResultPanel.openDuration - roundResultPanel.closeDuration);

        Debug.Log($"[GameManager] Applied server timing — " +
                  $"bettingDuration={bettingDuration}s " +
                  $"spinDuration={spinDuration}s " +
                  $"spinIntervalStart={spinIntervalStart}s " +
                  $"spinIntervalEnd={spinIntervalEnd}s " +
                  $"winGlowHoldDuration={winGlowHoldDuration}s " +
                  $"warmupDuration={warmupDuration}s");
    }

    // ── Item Registration ─────────────────────────────────────────────────────

    void RegisterItemsWithServer()
    {
        if (api == null) return;

        var items = new RegisterItemDto[betButtons.Count];
        for (int i = 0; i < betButtons.Count; i++)
        {
            var d = betButtons[i].data;
            items[i] = new RegisterItemDto
            {
                key        = d != null ? d.buttonID            : $"B{i + 1}",
                category   = d != null ? d.category.ToString() : "Pizza",
                multiplier = d != null ? d.winMultiplier       : 5f,
                label      = d != null ? $"x{d.winMultiplier:0.#}" : "x5",
            };
        }

        api.RegisterItems(items,
            onOk: () => Debug.Log("[GameManager] Item layout registered with dev server."),
            onErr: e  => Debug.LogWarning($"[GameManager] RegisterItems failed: {e}"));
    }

    // ── On-demand leaderboard ─────────────────────────────────────────────────

    public void OnViewLeaderboardClicked()
    {
        if (api == null || leaderboardPanel == null) return;
        StartCoroutine(ShowLeaderboardOnDemand());
    }

    public void OnViewRankingJackpotClicked()
    {
        if (rankingJackpotPanel != null)
        {
            rankingJackpotPanel.ShowPanel();
        }
    }

    public void OnRechargeClicked()
    {
        // Topping up is the HOST app's job — it owns the wallet, the payment
        // methods and the user's session. Ask it to open that screen rather
        // than trying to move money from inside the game.
        WebBridge.RequestRecharge();

        // The games backend's recharge endpoint is still called when one is
        // configured, so a deployment that used it keeps working. Unset (the
        // default) it only logs, which is why the button appeared to do nothing.
        if (api != null)
        {
            api.PostRecharge(
                onOk: _ => Debug.Log("[GameManager] Recharge request succeeded."),
                onErr: err => Debug.Log("[GameManager] No games-side recharge endpoint: " + err));
        }
    }

    public void OnViewLastResultClicked()
    {
        if (roundResultPanel == null || !lastRoundPlayed) return;
        StartCoroutine(ShowLastResultOnDemand(lastRoundEntries));
    }

    IEnumerator ShowLastResultOnDemand(List<LeaderboardEntry> entries)
    {
        yield return roundResultPanel.ShowAndWait(lastRoundWon, lastRoundWinnerIdx, lastRoundCombo, entries, lastRoundWonAmount, lastRoundParticipated, lastRoundBetAmount, lastRoundNumber);
    }

    IEnumerator ShowLeaderboardOnDemand()
    {
        List<LeaderboardEntry> entries = null;
        bool done = false;
        api.GetLeaderboard(dtos =>
        {
            if (dtos != null)
            {
                entries = new List<LeaderboardEntry>(dtos.Length);
                foreach (var d in dtos)
                    entries.Add(new LeaderboardEntry { rank = d.rank, username = d.username, wonAmount = d.wonAmount, todaysWin = d.todaysWin, avatar = d.avatar });
            }
            done = true;
        }, _ => done = true);
        float t = 0f;
        while (!done && t < 5f) { t += Time.deltaTime; yield return null; }

        if (entries != null)
            yield return leaderboardPanel.ShowAndWait(entries);
    }

    // ── Watchdog ──────────────────────────────────────────────────────────────

    IEnumerator ButtonActiveWatchdog()
    {
        while (true)
        {
            for (int i = 0; i < betButtons.Count; i++)
            {
                if (betButtons[i] != null && !betButtons[i].gameObject.activeSelf)
                {
                    Debug.LogWarning($"[Watchdog] Button {i} was set INACTIVE externally. Re-activating.");
                    betButtons[i].gameObject.SetActive(true);
                }
            }
            yield return null;
        }
    }

    // ── Server Round Loop ─────────────────────────────────────────────────────

    IEnumerator ServerRoundLoop()
    {
        while (true)
        {
            // 1. Fetch current round
            bool done = false;
            api.GetCurrentRound(r => { OnServerRound(r); done = true; }, _ => { done = true; });
        
            // 2. ALSO periodically fetch config to catch runtime changes
            api.GetConfig(ApplyServerConfig); 
        
            // 3. Fetch balance
            api.GetBalance(ApplyBalance);

            yield return new WaitForSeconds(serverPollInterval);
        }
    }

    void OnServerRound(GameRoundDto r)
    {
        if (r == null) return;

        SyncServerClock(r);

        // Drop stale / out-of-order polls for a round we've already advanced past
        // — otherwise a late poll for the previous round flips the label backward
        // (e.g. Bet Time → Show Time) for a frame, which reads as a glitch.
        if (r.roundNumber > 0 && r.roundNumber < serverRoundNumber) return;

        if (r.betsByItem != null)
        {
            // A new round's pool starts from scratch. Drop the overlay here
            // rather than in ResetForServerRound() alone: the badges below are
            // refreshed before the phase switch gets round to calling it, so a
            // stale overlay would show for a frame on the first poll of a round.
            if (r.roundNumber > 0 && r.roundNumber != serverRoundNumber)
                optimisticBetsByItem.Clear();

            lastBetsByItem = new Dictionary<string, long>(r.betsByItem);

            // Global (all-players) total bet badge — rides along with this same
            // poll GameManager already makes for the timer/phase sync, so this
            // costs zero extra network requests.
            for (int i = 0; i < betButtons.Count; i++)
            {
                string key = betButtons[i].data != null ? betButtons[i].data.buttonID : betButtons[i].name;
                betButtons[i].SetPooledBet(PooledBetForKey(key));
            }
        }

        // Refresh winMode from every poll — admin can change it mid-session
        // and a one-shot /config fetch would go stale.
        if (!string.IsNullOrEmpty(r.winMode)) serverWinMode = r.winMode;

        SyncHotItem(r.hotItem);

        // Round number label — driven straight off this poll's round number
        // (not the "betting"-phase-only serverRoundNumber) so it's never stale.
        if (currentRoundText != null && r.roundNumber > 0)
            currentRoundText.text = $"Today: Round {r.roundNumber}";

        switch (r.phase)
        {
            case "betting":
                if (r.roundNumber != serverRoundNumber)
                {
                    serverRoundNumber = r.roundNumber;

                    if (resolveResultRoutine != null)
                    {
                        StopCoroutine(resolveResultRoutine);
                        resolveResultRoutine = null;
                    }
                    if (roundResultPanel != null) roundResultPanel.Hide();

                    ResetForServerRound();

                    if (bettingStartBanner != null)
                        bettingStartBanner.ShowBanner();
                    else
                        Debug.LogError("[GameManager] bettingStartBanner is NULL! Assign it in the Inspector.");
                }
                serverBettingOpen    = true;
                serverSpinning       = false;
                serverShowTime       = false;
                serverBettingEndsUtc = r.BettingClosesAtUtc;
                phase = Phase.Betting;
                SetBetButtonsInteractable(true);
                SetRepeatInteractable(lastBetAmount > 0);
                SetStatus("Bet Time");
                foreach (var btn in betButtons) btn.SetBrightOverlayActive(false);
                break;

            case "closed":
                serverBettingOpen = false;
                serverSpinning    = false;
                serverShowTime    = false;
                SetBetButtonsInteractable(false);
                SetRepeatInteractable(false);
                foreach (var btn in betButtons) btn.SetBrightOverlayActive(false);
                break;

            case "spinning":
                serverBettingOpen = false;
                serverShowTime    = false;
                // Drive the Drawing countdown from the DB round's spinEndsAt.
                serverSpinning    = true;
                serverSpinEndsUtc = r.SpinEndsAtUtc;
                SetBetButtonsInteractable(false);
                SetRepeatInteractable(false);
                foreach (var btn in betButtons) btn.SetBrightOverlayActive(true);
                if (serverSpunRound != r.roundNumber)
                {
                    var winners = WinningButtonIndices(r);
                    if (winners.Count > 0)
                    {
                        serverSpunRound = r.roundNumber;

                        if (spinToWinnerRoutine != null)
                            StopCoroutine(spinToWinnerRoutine);

                        // Pass the absolute server end-time, not a pre-measured budget.
                        // The remaining time is recomputed right before animating so the
                        // chase always lands exactly at spinEndsAt, immune to poll jitter.
                        spinToWinnerRoutine = StartCoroutine(
                            SpinToWinnerAfterPreviousResult(winners, r.SpinEndsAtUtc));
                    }
                }
                break;

            case "result":
            case "completed":
                serverBettingOpen = false;
                serverSpinning    = false;
                serverShowTime    = false;
                SetBetButtonsInteractable(false);
                foreach (var btn in betButtons) btn.SetBrightOverlayActive(true);
                if (serverLitRound != r.roundNumber)
                {
                    var winners = WinningButtonIndices(r);
                    if (winners.Count > 0)
                    {
                        serverLitRound = r.roundNumber;

                        if (spinToWinnerRoutine == null)
                        {
                            var winSet = new HashSet<int>(winners);
                            for (int i = 0; i < betButtons.Count; i++)
                                betButtons[i].SetLit(winSet.Contains(i));
                        }
                    }
                }
                if (serverPoppedRound != r.roundNumber && !string.IsNullOrEmpty(r.winCategory))
                {
                    serverPoppedRound = r.roundNumber;
                    resolveResultRoutine = StartCoroutine(ResolveServerResultTracked(r));
                }
                break;

            case "warmup":
                serverBettingOpen = false;
                serverSpinning    = false;
                // Show Time: the win/lose panel window — tick down to warmupEndsAt.
                serverShowTime    = true;
                serverShowEndsUtc = r.WarmupEndsAtUtc;
                SetBetButtonsInteractable(false);
                SetRepeatInteractable(false);
                phase = Phase.Warmup;
                foreach (var btn in betButtons) btn.SetBrightOverlayActive(true);

                if (serverLitRound != r.roundNumber)
                {
                    var winners = WinningButtonIndices(r);
                    if (winners.Count > 0)
                    {
                        serverLitRound = r.roundNumber;
                        var winSet = new HashSet<int>(winners);
                        for (int i = 0; i < betButtons.Count; i++)
                            betButtons[i].SetLit(winSet.Contains(i));
                    }
                }

                if (serverPoppedRound != r.roundNumber && !string.IsNullOrEmpty(r.winCategory))
                {
                    serverPoppedRound = r.roundNumber;
                    resolveResultRoutine = StartCoroutine(ResolveServerResultTracked(r));
                }
                break;
        }
    }

    string lastHotItemKey = null;

    /// <summary>Shows the hot-item badge on whichever button matches the server's
    /// current hotItem key for this round, and hides it everywhere else. Safe to
    /// call every poll -- a no-op once the key stops changing.</summary>
    void SyncHotItem(string hotItemKey)
    {
        if (hotItemKey == lastHotItemKey) return;
        lastHotItemKey = hotItemKey;

        for (int i = 0; i < betButtons.Count; i++)
        {
            var btn = betButtons[i];
            if (btn == null) continue;
            string key = btn.data != null ? btn.data.buttonID : null;
            btn.SetHotItem(!string.IsNullOrEmpty(hotItemKey) && key == hotItemKey);
        }
    }

    void ResetForServerRound()
    {
        myBetsByItem.Clear();
        optimisticBetsByItem.Clear();
        for (int i = 0; i < betButtons.Count; i++)
        {
            betButtons[i].ResetBet();
            betButtons[i].SetPooledBet(0);
            betButtons[i].ResetVisuals();
            // Betting phase: the BettingHandPointer owns the glow, so clear the
            // glow ResetVisuals (SetLit(true)) just turned on.
            betButtons[i].ShowGlow(false);
            betButtons[i].SetBrightOverlayActive(false);
        }
        if (bearAnimator != null) bearAnimator.PlayIdle();
        phase = Phase.Betting;
    }

    List<int> WinningButtonIndices(GameRoundDto r)
    {
        var result = new List<int>();

        // Trust the server's own winning item key(s) instead of re-deriving a
        // "which one" guess from category + round number — that guess had no
        // guaranteed relationship to what the server actually rolled, so the
        // spin/panel could land on a different item than what was actually paid
        // out and recorded.
        string[] keys = (r.winMode == "combo")
            ? r.winningItems
            : (!string.IsNullOrEmpty(r.winningItem) ? new[] { r.winningItem } : null);

        if (keys == null || keys.Length == 0)
            return result;

        foreach (var key in keys)
        {
            for (int i = 0; i < betButtons.Count; i++)
            {
                if (betButtons[i].data != null && betButtons[i].data.buttonID == key)
                {
                    result.Add(i);
                    break;
                }
            }
        }
        return result;
    }

    IEnumerator SpinToWinnerAfterPreviousResult(List<int> winnerIndices, System.DateTime spinEndsUtc)
    {
        while (resolveResultRoutine != null)
            yield return null;

        yield return StartCoroutine(SpinToWinner(winnerIndices, spinEndsUtc));
    }

    IEnumerator ResolveServerResultTracked(GameRoundDto r)
    {
        
        long totalBetsSnapshot = TotalMyBets();

        while (spinToWinnerRoutine != null)
            yield return null;

        phase = Phase.Result;
        


        var winners = WinningButtonIndices(r);

        // ── RECORD HISTORY (SERVER AUTHORITATIVE) ──────
        if (historyTracker != null && winners != null && winners.Count > 0)
            RefreshHistoryFromServer();
        
        yield return StartCoroutine(ResolveServerResult(r));
        resolveResultRoutine = null;
    }

    void RefreshHistoryFromServer()
    {
        if (api == null || historyTracker == null) return;

        api.GetHistory(historyKeys =>
        {
            if (historyKeys == null) return;

            List<Sprite> historySprites = new List<Sprite>();
            foreach (var key in historyKeys)
            {
                Sprite sprite = FindSpriteForItemKey(key);
                if (sprite != null) historySprites.Add(sprite);
            }

            historyTracker.SetHistory(historySprites);
        }, err => Debug.LogWarning($"[GameManager] Failed to sync round history: {err}"));
    }

    IEnumerator SpinToWinner(List<int> winnerIndices, System.DateTime spinEndsUtc)
    {
        phase = Phase.Spinning;
        SetStatus("Drawing");
        if (bearAnimator != null) bearAnimator.PlayEat();

        int count     = betButtons.Count;
        int winnerIdx = winnerIndices[0];
        foreach (var b in betButtons) b.SetLit(false);

        // Remaining spin time measured NOW (after any wait for the previous round's
        // panel) so the chase lands exactly when the server ends the spin window.
        float budgetSeconds = (float)(spinEndsUtc - ServerUtcNow).TotalSeconds;
        // Guard against a late/negative budget (e.g. the spinning poll arrived
        // after spinEndsAt) so we still play a short chase instead of snapping.
        if (budgetSeconds < 0.5f) budgetSeconds = 0.5f;

        int startLit = Random.Range(0, count);
        int currentLit = startLit;

        foreach (float interval in BuildSpinIntervals(startLit, winnerIdx, count, budgetSeconds))
        {
            betButtons[currentLit].SetLit(true);
            if (audioManager != null) audioManager.PlayTick();
            yield return new WaitForSeconds(interval * 0.8f);
            betButtons[currentLit].SetLit(false);
            yield return new WaitForSeconds(interval * 0.2f);

            currentLit = (currentLit + 1) % count;
        }

        var winSet = new HashSet<int>(winnerIndices);
        for (int i = 0; i < count; i++) betButtons[i].SetLit(winSet.Contains(i));

        if (spinToWinnerRoutine != null)
            spinToWinnerRoutine = null;
    }

    float[] BuildSpinIntervals(int startLit, int winnerIdx, int count, float budgetSeconds)
    {
        int stepsToWinnerFromStart = ((winnerIdx - startLit) % count + count) % count;

        // Average tick of the curve is approximately (start + end) / 2
        float avgInterval = 0.5f * (spinIntervalStart + spinIntervalEnd);
        if (avgInterval <= 0f) avgInterval = 0.15f;

        int approxSteps = Mathf.Max(1, Mathf.RoundToInt(budgetSeconds / avgInterval));

        // Snap to the NEAREST step count that still lands on the winner (whole laps
        // + offset) so the natural duration stays as close to the budget as
        // possible — no rescaling, so the configured intervals stay honest.
        int residue    = (((stepsToWinnerFromStart - approxSteps) % count) + count) % count;
        int totalSteps = approxSteps + residue;
        if (residue > count / 2) totalSteps -= count;   // round to nearest, not always up
        
        // Guarantee at least 3 full laps for a longer perceived spin
        while (totalSteps < count * 3) totalSteps += count; 

        // Ease-in-out tick values: start (fast) → end (slow). 
        var intervals = new float[totalSteps];
        float sum = 0f;
        for (int step = 0; step < totalSteps; step++)
        {
            float t = totalSteps > 1 ? (float)step / (totalSteps - 1) : 1f;
            float easedT = t;
            
            switch (spinCurve)
            {
                case SpinCurveType.Linear:
                    easedT = t;
                    break;
                case SpinCurveType.EaseIn:
                    easedT = t * t;
                    break;
                case SpinCurveType.EaseOut:
                    easedT = t * (2f - t);
                    break;
                case SpinCurveType.EaseInOut:
                    easedT = Mathf.SmoothStep(0f, 1f, t);
                    break;
            }
            
            intervals[step] = Mathf.Lerp(spinIntervalStart, spinIntervalEnd, easedT);
            sum += intervals[step];
        }

        float scale = (sum > 0f) ? budgetSeconds / sum : 1f;
        for (int step = 0; step < totalSteps; step++) intervals[step] *= scale;
        return intervals;
    }

    IEnumerator ResolveServerResult(GameRoundDto r)
    {
        long totalBetsSnapshot = TotalMyBets();

        while (spinToWinnerRoutine != null)
            yield return null;

        phase = Phase.Result;

        var winners = WinningButtonIndices(r);

        string winKeys = string.Join(", ", winners.ConvertAll(i =>
            betButtons[i].data != null ? betButtons[i].data.buttonID : betButtons[i].name));
        Debug.Log($"[GameManager] Round {r.roundNumber} winner(s): {winKeys} | category={r.winCategory} | winMode={r.winMode}");

        long won = 0;
        foreach (int i in winners)
        {
            var data = betButtons[i].data;
            if (data == null) continue;
            if (myBetsByItem.TryGetValue(data.buttonID, out long stake) && stake > 0)
                won += (long)(stake * data.winMultiplier);
        }

        if (won > 0)
        {
            if (bearAnimator != null) bearAnimator.PlayWin();
            if (audioManager  != null) audioManager.PlayWin();
            
            // --- NEW: Tell the tracker to increase the win count ---
            if (todaysWinTracker != null) 
                todaysWinTracker.IncrementWinCount();
        }
        else if (totalBetsSnapshot > 0)
        {
            if (audioManager != null) audioManager.PlayLose();
        }

        // Post-draw "Show Time" window — the RoundResultPanel shows the win amount.
        SetStatus("Show Time");

        // In that moment, flow all coins to the jar
        Coroutine c1 = null, c2 = null;
        if (coinFlyEffect != null) c1 = coinFlyEffect.FlowAllCoinsToJar(jarGameObject);
        if (fakeBetCoinShower != null) c2 = fakeBetCoinShower.FlowAllCoinsToJar(jarGameObject);

        // Each button's Global Total Bet Badge coin flies to the jar too —
        // individual bet coins destroy on landing now, so this badge icon is
        // what visually carries the pooled bet into the jar at round end.
        List<Coroutine> globalBetFlights = new List<Coroutine>();
        for (int i = 0; i < betButtons.Count; i++)
        {
            Coroutine c = betButtons[i].FlyGlobalBetCoinToJar(jarGameObject);
            if (c != null) globalBetFlights.Add(c);
        }

        if (c1 != null) yield return c1;
        if (c2 != null) yield return c2;
        foreach (var c in globalBetFlights) yield return c;

        if (api != null)
        {
            bool done = false;
            api.GetBalance(b => { ApplyBalance(b); done = true; }, _ => done = true);
            float bt = 0f;
            while (!done && bt < 5f) { bt += Time.deltaTime; yield return null; }
        }

        // ── Show RoundResultPanel only when the player actually placed a bet. ──────
        bool participated = totalBetsSnapshot > 0;
        bool isCombo = (r.winMode == "combo");
        int  winIdx  = winners.Count > 0 ? winners[0] : -1;
        
        List<LeaderboardEntry> fetchedEntries = null;
        if (roundResultPanel != null)
        {
            if (api != null)
            {
                bool lbDone = false;
                // THIS round's winners (not the all-day leaderboard).
                api.GetRoundLeaderboard(dtos =>
                {
                    if (dtos != null && dtos.Length > 0)
                    {
                        fetchedEntries = new List<LeaderboardEntry>(dtos.Length);
                        foreach (var d in dtos)
                            fetchedEntries.Add(new LeaderboardEntry { rank = d.rank, username = d.username, wonAmount = d.wonAmount, todaysWin = d.todaysWin, avatar = d.avatar });
                    }
                    else
                    {
                        Debug.LogWarning("[GameManager] GetRoundLeaderboard succeeded but returned no entries -- podium will be empty this round.");
                    }
                    lbDone = true;
                }, err =>
                {
                    Debug.LogWarning($"[GameManager] GetRoundLeaderboard failed -- podium will be empty this round: {err}");
                    lbDone = true;
                });

                float lbTime = 0f;
                while (!lbDone && lbTime < 5f) { lbTime += Time.deltaTime; yield return null; }
                if (!lbDone)
                    Debug.LogWarning("[GameManager] GetRoundLeaderboard timed out after 5s -- podium will be empty this round.");
            }
        }

        // Caching last round results
        lastRoundPlayed = true;
        lastRoundWon = won > 0;
        lastRoundWinnerIdx = winIdx;
        lastRoundCombo = isCombo;
        lastRoundWonAmount = won;
        lastRoundParticipated = participated;
        lastRoundBetAmount = totalBetsSnapshot;
        lastRoundNumber = r.roundNumber;
        lastRoundEntries = fetchedEntries;

        if (roundResultPanel != null)
        {
            // 3. Show the panel, passing in the fetched data!
            yield return roundResultPanel.ShowAndWait(won > 0, winIdx, isCombo, fetchedEntries, won, participated, totalBetsSnapshot, r.roundNumber);
        }

        // ── Panel closed — reset visuals and finish winner glow ──────────────────
        ResetForServerRound();
    }

    long TotalMyBets()
    {
        long s = 0;
        foreach (var kv in myBetsByItem) s += kv.Value;
        return s;
    }

    // ── Bet Button Clicks ─────────────────────────────────────────────────────

    void OnBetButtonClicked(int idx)
    {
        if (!serverBettingOpen) return;
        if (selectedBetAmount <= 0) return;
        if (playerCoins < selectedBetAmount) { SetStatus("Not enough coins!"); return; }

        string key    = betButtons[idx].data != null ? betButtons[idx].data.buttonID : betButtons[idx].name;
        long   amount = selectedBetAmount;

        playerCoins -= amount;
        myBetsByItem[key] = (myBetsByItem.TryGetValue(key, out var cur) ? cur : 0) + amount;
        lastBetAmount = amount;
        betButtons[idx].AddBet(amount, true);

        // Your stake shows in the pooled TOTAL now, not on the next poll.
        // Bumping BOTH myBetsByItem (above) and the pooled overlay by the same
        // amount keeps FakeBetCoinShower's `others = pooled - mine` unchanged,
        // so your own bet never triggers a phantom shower of other players' coins.
        optimisticBetsByItem[key] = PooledBetForKey(key) + amount;
        betButtons[idx].SetPooledBet(PooledBetForKey(key));

        UpdateCoinUI();
        DoCoinFly(idx);

        api.PostBet(key, amount,
            _ => { if (api != null) api.GetBalance(ApplyBalance); },
            err =>
            {
                playerCoins += amount;
                if (myBetsByItem.TryGetValue(key, out var c))
                    myBetsByItem[key] = System.Math.Max(0, c - amount);
                betButtons[idx].AddBet(-amount, true);

                optimisticBetsByItem[key] = System.Math.Max(0, PooledBetForKey(key) - amount);
                betButtons[idx].SetPooledBet(PooledBetForKey(key));

                UpdateCoinUI();
                SetStatus(err);
            });
    }

    void DoCoinFly(int idx)
    {
        if (coinFlyEffect != null && selectedAmountIndex >= 0 && selectedAmountIndex < amountButtons.Count)
        {
            var sourceCoin  = amountButtons[selectedAmountIndex];
            var sourceRect  = sourceCoin.GetComponent<RectTransform>();
            var targetRect  = betButtons[idx].GetComponent<RectTransform>();

            // Use the same coin sprite as FakeBetCoinShower's "other players'
            // bets" coins, so the player's own flying coin matches -- instead of
            // the selected amount button's own (denomination-specific) sprite.
            Sprite flySprite = (fakeBetCoinShower != null && fakeBetCoinShower.coinSprites.Count > 0)
                               ? fakeBetCoinShower.coinSprites[0]
                               : (sourceCoin.IsSelected && sourceCoin.buttonImage != null
                                   ? sourceCoin.buttonImage.sprite
                                   : sourceCoin.normalSprite);

            coinFlyEffect.PlayCoinFly(sourceRect, targetRect, flySprite);
        }
    }

    void SelectAmount(int idx)
    {
        if (idx < 0 || idx >= amountButtons.Count) return;
        selectedBetAmount   = amountButtons[idx].amount;
        selectedAmountIndex = idx;
        for (int i = 0; i < amountButtons.Count; i++)
            amountButtons[i].SetSelected(i == idx);
    }

    void OnRepeatClicked()
    {
        bool open = serverBettingOpen;
        if (!open || lastBetAmount <= 0) return;
        selectedBetAmount = lastBetAmount;

        for (int i = 0; i < amountButtons.Count; i++)
        {
            bool match = amountButtons[i].amount == lastBetAmount;
            amountButtons[i].SetSelected(match);
            if (match) selectedAmountIndex = i;
        }

        SetStatus($"Bet amount set to {FormatCoins(selectedBetAmount)}");
    }

    void UpdateCoinUI()
    {
        if (coinBalanceText != null)
            coinBalanceText.text = FormatCoins(playerCoins);
    }

    void SetStatus(string msg)
    {
        if (statusText != null) statusText.text = msg;
    }

    void UpdateMatchStatusText()
    {
        if (StatusText == null) return;

        // Player-facing 3-phase loop only — no internal phase jargon:
        //   Betting            → "Bet Time"
        //   Spinning           → "Drawing"
        //   Result/Warmup/Countdown/Idle → "Show Time"
        switch (_phase)
        {
            case Phase.Betting:
                StatusText.text = "Bet Time";
                break;
            case Phase.Spinning:
                StatusText.text = "Drawing";
                break;
            default: // Result, Warmup, Countdown, Idle
                StatusText.text = "Show Time";
                break;
        }
    }

    void SetBetButtonsInteractable(bool on)
    {
        foreach (var btn in betButtons)
        {
            var b = btn.GetComponent<Button>();
            if (b != null) b.interactable = on;
        }
    }

    void SetRepeatInteractable(bool on)
    {
        if (repeatButton != null) repeatButton.interactable = on;
    }

    /// <summary>
    /// What GameRecordCard's Reward Details column should show for a past round:
    /// the single combo sprite (salad/pizza) if more than one item paid out that
    /// round, matching RoundResultPanel's combo display. Returns null for a plain
    /// single-item win, so the caller falls back to that item's own icon.
    /// </summary>
    public Sprite GetComboRewardSprite(List<string> winningFruits)
    {
        if (winningFruits == null || winningFruits.Count <= 1) return null;

        string firstKey = winningFruits[0];
        foreach (var btn in betButtons)
        {
            if (btn != null && btn.data != null && btn.data.buttonID == firstKey)
                return btn.data.category == BetCategory.Vegetable ? comboSaladSprite : comboPizzaSprite;
        }
        return null;
    }

    /// <summary>
    /// The combo sprite (salad/pizza) for a winning bet-button index. Derived from
    /// the button's own BetCategory rather than assuming a fixed index range, so it
    /// stays correct no matter what order the buttons are listed in the Inspector.
    /// </summary>
    public Sprite GetComboSpriteByIndex(int index)
    {
        if (index < 0 || index >= betButtons.Count) return null;
        var btn = betButtons[index];
        if (btn == null || btn.data == null) return null;
        return btn.data.category == BetCategory.Vegetable ? comboSaladSprite : comboPizzaSprite;
    }

    public Sprite FindSpriteForItemKey(string key)
    {
        if (key == "combo_Salad" || key == "combo_salad" || key == "combo_Vegetable" || key == "combo_vegetable")
            return comboSaladSprite;
        if (key == "combo_Pizza" || key == "combo_pizza")
            return comboPizzaSprite;

        foreach (var btn in betButtons)
        {
            if (btn == null) continue;
            string btnKey = btn.data != null ? btn.data.buttonID : btn.name;
            if (btnKey == key)
                return btn.data != null ? btn.data.historyIcon : null;
        }
        return null;
    }

    static string FormatCoins(long v)
    {
        if (v >= 1_000_000) return $"{v / 1_000_000f:0.#}M";
        if (v >= 1_000)     return $"{v / 1_000f:0.#}k";
        return v.ToString();
    }

    public Sprite GetFoodSpriteByIndex(int index)
    {
        if (index >= 0 && index < foodSprites.Count)
        {
            return foodSprites[index];
        }
        return null;
    }

    public Sprite GetFoodSpriteByID(string buttonID)
    {
        if (string.IsNullOrEmpty(buttonID)) return null;

        // Match by the button's own buttonID (not its position) and use its
        // dedicated historyIcon, so this stays correct regardless of what
        // order the buttons/sprites happen to be listed in the Inspector.
        for (int i = 0; i < betButtons.Count; i++)
        {
            if (betButtons[i] != null && betButtons[i].data != null && betButtons[i].data.buttonID == buttonID)
            {
                return betButtons[i].data.historyIcon;
            }
        }
        return null;
    }

    public void OpenGameRecords()
    {
        if (gameRecordsPanel == null) return;
        
        // Instantly open the panel to show the transition and Loading status
        gameRecordsPanel.Open();

        if (api == null) return;

        api.GetGameRecords(dtos =>
        {
            List<GameRecord> records = new List<GameRecord>();
            if (dtos != null)
            {
                foreach (var d in dtos)
                {
                    List<BetSelection> selectedFood = new List<BetSelection>();
                    if (d.selectedFood != null)
                    {
                        foreach (var s in d.selectedFood)
                            selectedFood.Add(new BetSelection { key = s.key, amount = s.amount });
                    }
                    records.Add(new GameRecord
                    {
                        roundNumber = d.roundNumber,
                        timestamp = d.timestamp,
                        selectedFood = selectedFood,
                        winningFruits = d.winningFruits != null ? new List<string>(d.winningFruits) : new List<string>(),
                        winCoins = d.winCoins,
                        balanceBefore = d.balanceBefore,
                        balanceAfter = d.balanceAfter
                    });
                }
            }
            gameRecordsPanel.Populate(records);
        }, err => {
            if (gameRecordsPanel.statusMessageText != null)
            {
                gameRecordsPanel.statusMessageText.text = "Failed to load: " + err;
            }
        });
    }
}
