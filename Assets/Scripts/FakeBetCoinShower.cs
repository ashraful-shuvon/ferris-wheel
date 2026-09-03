using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class FakeBetCoinShower : MonoBehaviour
{
    [Header("References")]
    public RectTransform coinPrefab;
    public RectTransform coinParent;
    public RectTransform sourceTransform;
    public List<BetButton> betButtons = new List<BetButton>();
    public GameManager gameManager;

    [Header("Coin Sprites")]
    public List<Sprite> coinSprites = new List<Sprite>();

    [Header("Spawn Settings")]
    public int   coinsPerStack    = 5;
    public float coinStaggerDelay = 0.08f;

    [Header("Flight Settings")]
    public float flightDuration = 0.7f;
    public Ease  flightEase     = Ease.InQuad;
    public float sourceSpread   = 15f;
    public float targetSpread   = 15f;

    [Header("Coin Visual")]
    public Vector2 coinSize       = new Vector2(40f, 40f);
    public float   coinStartScale = 0.3f;

    [Header("Jar Flow Settings")]
    public float jarFlightDuration = 0.6f;

    [Header("Spawn Under Button")]
    [Tooltip("If true, each coin is spawned as a child of the button it flies toward. " +
             "This means the coin renders at the button's layer in the hierarchy, " +
             "sitting behind or in front based on the button's child order. " +
             "If false, coins are spawned under coinParent as before.")]
    public bool spawnUnderTargetButton = true;

    [Header("Real Other-Player Bets")]
    [Tooltip("Show coins for real other-player bets, derived from the server's " +
             "pooled bets minus my own. Turn off to disable this visualization " +
             "(e.g. while using the Cycle Demo below instead).")]
    public bool showRealOtherBets = true;

    [Header("Cycle Demo (ambient, toggleable)")]
    [Tooltip("When on, a coin stack visually cycles through every bet button in " +
             "order (B1 -> B8 -> loop), holding briefly on each before moving to " +
             "the next. Purely visual -- does not touch bet totals. Independent " +
             "of showRealOtherBets, so both could technically run at once.")]
    public bool enableCycleDemo = false;
    [Tooltip("How long the coin(s) stay visible on each button before moving to the next.")]
    public float cycleHoldDuration = 1f;
    [Tooltip("Pause after a full B1-B8 pass completes, before starting the next pass from B1.")]
    public float cycleRestartDelay = 1.5f;

    const float ServerPollInterval = 0.4f;
    Coroutine showerRoutine;
    Coroutine cycleDemoRoutine;

    readonly List<RectTransform> pool            = new List<RectTransform>();
    readonly List<RectTransform> stationaryCoins = new List<RectTransform>();

    Dictionary<string, long> prevOthers = new Dictionary<string, long>();
    int trackedRound = -1;

    void Start()
    {
        if (coinPrefab == null)      { Debug.LogError("[FakeBetCoinShower] coinPrefab not assigned!");      enabled = false; return; }
        if (sourceTransform == null) { Debug.LogError("[FakeBetCoinShower] sourceTransform not assigned!"); enabled = false; return; }
        if (coinParent == null)      { Debug.LogError("[FakeBetCoinShower] coinParent not assigned!");      enabled = false; return; }

        coinPrefab.gameObject.SetActive(false);
        showerRoutine = StartCoroutine(ShowerLoop());
        cycleDemoRoutine = StartCoroutine(CycleDemoLoop());
    }

    void OnDestroy()
    {
        if (showerRoutine != null) StopCoroutine(showerRoutine);
        if (cycleDemoRoutine != null) StopCoroutine(cycleDemoRoutine);
    }

    IEnumerator ShowerLoop()
    {
        while (true)
        {
            // Real other-player bets, derived from the pooled bets minus my own.
            // Playing alone → no others → no coins. Toggleable off entirely.
            if (showRealOtherBets)
                ShowRealOtherBets();
            yield return new WaitForSeconds(ServerPollInterval);
        }
    }

    /// <summary>Ambient demo: cycles a coin stack through every bet button in
    /// order, holding on each before moving to the next. Coins stay sitting on
    /// their buttons exactly like a real bet's coins do (added to the same
    /// stationaryCoins pool) -- they only fly to the jar via the normal
    /// round-complete FlowAllCoinsToJar call, same as before. Only runs during
    /// Bet Time -- paused during Drawing/Show Time. Re-checks every loop, so
    /// flipping the toggle (or the round phase changing) takes effect live.</summary>
    IEnumerator CycleDemoLoop()
    {
        while (true)
        {
            bool canRun = enableCycleDemo && betButtons.Count > 0
                          && gameManager != null && gameManager.IsBetting;
            if (!canRun)
            {
                yield return new WaitForSeconds(0.25f);
                continue;
            }

            for (int i = 0; i < betButtons.Count; i++)
            {
                if (!enableCycleDemo || gameManager == null || !gameManager.IsBetting) break;

                var btn = betButtons[i];
                if (btn == null) continue;

                yield return StartCoroutine(SpawnStack(btn));
                yield return new WaitForSeconds(cycleHoldDuration);
            }

            // Full B1-B8 pass complete -- pause before starting over from B1.
            yield return new WaitForSeconds(cycleRestartDelay);
        }
    }

    void ShowRealOtherBets()
    {
        if (!gameManager.IsBetting || betButtons.Count == 0) return;

        if (gameManager.ServerRoundNumber != trackedRound)
        {
            trackedRound = gameManager.ServerRoundNumber;
            prevOthers.Clear();
        }

        foreach (var btn in betButtons)
        {
            if (btn == null || btn.data == null) continue;
            string key = btn.data.buttonID;

            long others = gameManager.PooledBetForKey(key) - gameManager.MyBetForKey(key);
            if (others < 0) others = 0;

            long prev  = prevOthers.TryGetValue(key, out var p) ? p : 0;
            long delta = others - prev;
            prevOthers[key] = others;

            if (delta > 0)
            {
                btn.AddBet(delta);
                StartCoroutine(SpawnStack(btn));
            }
        }
    }

    IEnumerator SpawnStack(BetButton targetBtn)
    {
        for (int i = 0; i < coinsPerStack; i++)
        {
            SpawnCoin(targetBtn);
            yield return new WaitForSeconds(coinStaggerDelay);
        }
    }

    void SpawnCoin(BetButton targetBtn)
    {
        RectTransform targetRect = targetBtn.GetComponent<RectTransform>();

        // Decide parent: under the button itself, or under coinParent.
        Transform spawnParent = (spawnUnderTargetButton && targetRect != null)
                              ? targetRect
                              : (Transform)coinParent;

        RectTransform coin = GetPooled(spawnParent);
        coin.gameObject.SetActive(true);
        coin.sizeDelta  = coinSize;
        coin.localScale = Vector3.one * coinStartScale;

        // Never let this coin render on top of the target button's badges (hot
        // item / total bet / global bet) — pin them back above it.
        if (spawnUnderTargetButton) targetBtn.KeepBadgesOnTop();

        if (coinSprites != null && coinSprites.Count > 0)
        {
            var img = coin.GetComponent<Image>();
            if (img != null)
            {
                img.sprite = coinSprites[Random.Range(0, coinSprites.Count)];
                Color c = img.color; c.a = 1f; img.color = c;
            }
        }

        // Positions in coinParent local space for the flight.
        Vector2 startPos = (Vector2)coinParent.InverseTransformPoint(sourceTransform.position)
                         + new Vector2(Random.Range(-sourceSpread, sourceSpread),
                                       Random.Range(-sourceSpread, sourceSpread));

        Vector2 endPos   = (Vector2)coinParent.InverseTransformPoint(targetRect.position)
                         + new Vector2(Random.Range(-targetSpread, targetSpread),
                                       Random.Range(-targetSpread, targetSpread));

        // Place the coin at the start in world space so it starts at the source
        // regardless of which parent it's under.
        coin.position = coinParent.TransformPoint(startPos);

        coin.DOScale(Vector3.one, flightDuration * 0.3f).SetEase(Ease.OutBack);

        // Fly to end in world space.
        Vector3 endWorldPos = coinParent.TransformPoint(endPos);
        coin.DOMove(endWorldPos, flightDuration)
            .SetEase(flightEase)
            .OnComplete(() => DestroyLandedCoin(coin));
    }

    /// <summary>Called the instant a fake-bet coin finishes flying onto a bet
    /// button. Coins are a one-shot visual flourish now, not a stacking pool
    /// kept around for the round-end jar flow, so they're destroyed on arrival
    /// instead of being added to stationaryCoins.</summary>
    void DestroyLandedCoin(RectTransform coin)
    {
        if (coin == null) return;
        coin.DOKill();
        pool.Remove(coin);
        Destroy(coin.gameObject);
    }

    public Coroutine FlowAllCoinsToJar(Transform jarTransform, float duration = -1f)
    {
        if (jarTransform == null) return null;
        float d = duration > 0f ? duration : jarFlightDuration;
        return StartCoroutine(FlowCoinsSequence(jarTransform, d));
    }

    IEnumerator FlowCoinsSequence(Transform jarTransform, float duration)
    {
        List<RectTransform> coinsToFly = new List<RectTransform>(stationaryCoins);
        stationaryCoins.Clear();

        foreach (var coin in coinsToFly)
        {
            if (coin == null || !coin.gameObject.activeSelf) continue;

            Image img = coin.GetComponent<Image>();
            if (img != null) { Color c = img.color; c.a = 1f; img.color = c; }

            RectTransform targetCoin = coin;
            coin.DOKill();
            coin.DOMove(jarTransform.position, duration)
                .SetEase(Ease.InBack)
                .OnComplete(() =>
                {
                    coin.DOScale(Vector3.zero, 0.1f)
                        .OnComplete(() => ReturnToPool(targetCoin));
                });
        }

        yield return new WaitForSeconds(duration + 0.15f);
    }

    // ── Pool — now takes a parent so coins are created under the right object ──

    RectTransform GetPooled(Transform parent)
    {
        // Look for an inactive pooled coin already under this parent.
        foreach (var c in pool)
            if (c != null && !c.gameObject.activeSelf && c.parent == parent)
                return c;

        // None found — instantiate a new one under the correct parent.
        RectTransform instance = Instantiate(coinPrefab, parent);
        instance.name = "FakeCoin";
        instance.SetSiblingIndex(2);
        pool.Add(instance);
        return instance;
    }

    void ReturnToPool(RectTransform coin)
    {
        coin.DOKill();
        coin.localScale = Vector3.one * coinStartScale;
        coin.gameObject.SetActive(false);
    }
}