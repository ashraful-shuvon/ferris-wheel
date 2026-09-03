using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// Attach this to each of the 8 bet buttons (B1–B8).
///
/// VISUAL RULE (per spec):
///   • Resting / idle / betting phase  → fully bright (overlay alpha 0)
///   • During spin, NOT the chase target → dimmed 20–30% (overlay alpha ~0.25)
///   • During spin, IS the chase target  → fully bright (overlay alpha 0)
///
/// The button GameObject and its Image are NEVER set inactive by this script.
/// Dimming is done purely with a dark overlay Image layered on top.
/// </summary>
public class BetButton : MonoBehaviour
{
    [Header("Data")]
    public BetButtonData data;

    [Header("UI References")]
    public Image buttonImage;
    public Text betAmountText;
    public GameObject glowEffect;

    [Tooltip("The 'TotalBet' container (Bet: label + coin icon + Number). " +
             "Hidden when the bet is 0, shown when the player has staked coins.")]
    public GameObject totalBetGroup;

    [Header("Dim Overlay")]
    [Tooltip("Plain black Image, child of this button, stretched to fill it. " +
             "Starts invisible. Script controls its alpha only — never SetActive.")]
    public Image dimOverlay;

    [Tooltip("Tint of the dim overlay. Only the RGB is used — the alpha is driven by dimOverlayAlpha. " +
             "Set this here, not on the DimOverlay Image: the script writes the overlay's colour every frame it changes state.")]
    public Color dimOverlayColor = Color.black;

    [Range(0f, 1f)]
    [Tooltip("How dark the overlay gets when this button is NOT the chase target during spin. " +
             "0.2–0.3 = 20–30% dim, 1 = fully opaque.")]
    public float dimOverlayAlpha = 0.25f;

    [HideInInspector] public long stackedBet = 0;
    [HideInInspector] public long myStackedBet = 0;
    
    [Header("Bright Overlay")]
    [Tooltip("White/yellow glow image stretched over the button. Used to boost brightness when active.")]
    public Image brightOverlay;

    [Range(0f, 1f)]
    public float brightOverlayAlpha = 0.35f;

    [Header("Hot Item Badge")]
    [Tooltip("Small badge shown in the button's top-right corner when the server " +
             "marks this item as the round's 'hot item'. Hidden otherwise.")]
    public GameObject hotItemBadge;

    [Header("Global Total Bet Badge")]
    [Tooltip("Badge showing the pooled bet from ALL players on this item, synced " +
             "from the server's round.betsByItem on every poll. Hidden when 0.")]
    public GameObject globalBetGroup;
    public Text globalBetText;

    [Tooltip("Coin icon inside the Global Total Bet Badge. Auto-resolved from a " +
             "child named 'CoinImage' under globalBetGroup if left unassigned.")]
    public RectTransform globalBetCoinImage;

    [Tooltip("How long this button's global-bet coin takes to fly into the jar at round end.")]
    public float globalBetJarFlightDuration = 0.6f;

    Vector3 globalBetCoinRestLocalPos;
    bool    globalBetCoinRestCaptured;

    void Awake()
    {
        ForceActive();

        SetupDimOverlay();
        SetupBrightOverlay();
        SetupGlobalBetCoinImage();

        SetGlow(false);
        RefreshBetLabel();
        SetLit(true);
    }

    void SetupGlobalBetCoinImage()
    {
        if (globalBetCoinImage == null && globalBetGroup != null)
        {
            var t = globalBetGroup.transform.Find("CoinImage");
            if (t != null) globalBetCoinImage = t as RectTransform;
        }

        if (globalBetCoinImage != null)
        {
            globalBetCoinRestLocalPos = globalBetCoinImage.localPosition;
            globalBetCoinRestCaptured = true;
        }
    }

    void OnEnable()
    {
        // If something disabled and re-enabled this object, restore correct visuals.
        ForceActive();
    }

    void OnDisable()
    {
        // Bug found & fixed: glowEffect was wired to "self" on some buttons, so
        // turning the glow off disabled the whole button. SetGlow() now guards
        // against this, so OnDisable should no longer fire during normal play.
        // Leaving this as a quiet warning in case something else ever causes it.
        Debug.LogWarning($"[BetButton] {gameObject.name} was disabled. " +
                          $"If this fires during normal play, something other than glowEffect is now the cause.");
    }

    void ForceActive()
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        if (buttonImage != null && !buttonImage.gameObject.activeSelf)
            buttonImage.gameObject.SetActive(true);
    }

    void SetupDimOverlay()
    {
        if (dimOverlay == null) return;
        dimOverlay.gameObject.SetActive(true);
        Color c = dimOverlayColor;
        c.a = 0f;
        dimOverlay.color = c;
        dimOverlay.raycastTarget = false;
    }

#if UNITY_EDITOR
    // Lets dimOverlayColor / dimOverlayAlpha be tuned live in the inspector.
    // Keeps whatever lit/dim state the button is currently in.
    void OnValidate()
    {
        if (dimOverlay == null) return;
        bool dimmed = dimOverlay.color.a > 0f;
        Color c = dimOverlayColor;
        c.a = dimmed ? dimOverlayAlpha : 0f;
        dimOverlay.color = c;
    }
#endif

    // ── Public API ──────────────────────────────────────────────────────────────

    /// <summary>
    /// true  = fully bright (overlay alpha 0)
    /// false = dimmed 20-30% (overlay alpha = dimOverlayAlpha)
    /// Never touches active state.
    /// </summary>
    public void SetLit(bool lit)
    {
        // Dim overlay
        if (dimOverlay != null)
        {
            Color c = dimOverlayColor;
            c.a = lit ? 0f : dimOverlayAlpha;
            dimOverlay.color = c;
        }

        // Bright overlay
        if (brightOverlay != null)
        {
            Color c = brightOverlay.color;

            if (lit)
            {
                c = Color.white;
                c.a = brightOverlayAlpha;
            }
            else
            {
                c.a = 0f;
            }

            brightOverlay.color = c;
        }

        SetGlow(lit);
    }

    public void ResetVisuals()
    {
        SetLit(true);
    }

    /// <summary>
    /// Toggle ONLY the glow effect, independent of lit/dim state. Used by
    /// BettingHandPointer to highlight the button the hand is resting on.
    /// </summary>
    public void ShowGlow(bool visible) => SetGlow(visible);

    public void AddBet(long amount, bool isLocalPlayer = false)
    {
        stackedBet += amount;
        if (isLocalPlayer)
        {
            myStackedBet += amount;
        }
        RefreshBetLabel();
    }

    public void ResetBet()
    {
        stackedBet = 0;
        myStackedBet = 0;
        RefreshBetLabel();
    }

    void RefreshBetLabel()
    {
        bool hasBet = myStackedBet > 0;

        // Hide the whole "Bet: 1000" group when nothing is staked; show it once
        // the player bets.
        if (totalBetGroup != null)
            totalBetGroup.SetActive(hasBet);

        if (betAmountText != null)
            betAmountText.text = hasBet ? FormatCoins(myStackedBet) : "";
    }

    void SetGlow(bool on)
    {
        if (glowEffect == null) return;

        // Safety: never allow this to disable the button's own GameObject. If
        // glowEffect was accidentally wired to "self" or a parent in the Inspector,
        // turning the glow off would disable the whole button (this was the bug).
        if (glowEffect == this.gameObject || this.transform.IsChildOf(glowEffect.transform))
        {
            Debug.LogWarning($"[BetButton] '{gameObject.name}' has glowEffect wired to itself or a parent! " +
                              $"Fix in the Inspector \u2014 glowEffect must be a separate CHILD object, not the button itself.");
            return;
        }

        glowEffect.SetActive(on);
    }
    
    void SetupBrightOverlay()
    {
        if (brightOverlay == null) return;

        brightOverlay.gameObject.SetActive(true);

        Color c = Color.white;
        c.a = 0f;

        brightOverlay.color = c;
        brightOverlay.raycastTarget = false;
    }

    public void SetBrightOverlayActive(bool active)
    {
        if (brightOverlay != null)
        {
            brightOverlay.gameObject.SetActive(active);
        }
    }

    /// <summary>Shows/hides the "hot item" corner badge for this button.</summary>
    public void SetHotItem(bool isHot)
    {
        if (hotItemBadge != null) hotItemBadge.SetActive(isHot);
    }

    /// <summary>
    /// Re-asserts the badges as the topmost children of this button so FakeCoin /
    /// FlyingCoin visuals landing on the button (which reparent under it and can
    /// set themselves as last sibling) never render on top of them. Call this
    /// right after any coin is spawned or reparented onto this button.
    /// </summary>
    public void KeepBadgesOnTop()
    {
        if (hotItemBadge   != null) hotItemBadge.transform.SetAsLastSibling();
        if (totalBetGroup  != null) totalBetGroup.transform.SetAsLastSibling();
        if (globalBetGroup != null) globalBetGroup.transform.SetAsLastSibling();
    }

    /// <summary>
    /// Updates the global (all-players) total bet badge from the server's pooled
    /// betsByItem value for this item. Call on every round poll — cheap no-op UI
    /// update, no network cost of its own since the value rides along with the
    /// round poll GameManager already makes.
    /// </summary>
    public void SetPooledBet(long amount)
    {
        bool hasPool = amount > 0;
        if (globalBetGroup != null) globalBetGroup.SetActive(hasPool);
        if (globalBetText != null) globalBetText.text = hasPool ? FormatCoins(amount) : "";
    }

    /// <summary>
    /// Flies this button's Global Total Bet Badge coin into the jar at round end —
    /// the pooled-bet equivalent of the FakeBetCoinShower/CoinFlyEffect coin sweep,
    /// since individual bet coins are now destroyed the instant they land instead
    /// of sticking around to fly to the jar themselves. Returns null (nothing to
    /// animate) when this button has no pooled bet showing.
    /// </summary>
    public Coroutine FlyGlobalBetCoinToJar(Transform jarTransform, float duration = -1f)
    {
        if (jarTransform == null || globalBetCoinImage == null ||
            globalBetGroup == null || !globalBetGroup.activeSelf)
            return null;

        return StartCoroutine(FlyGlobalBetCoinRoutine(jarTransform,
            duration > 0f ? duration : globalBetJarFlightDuration));
    }

    IEnumerator FlyGlobalBetCoinRoutine(Transform jarTransform, float duration)
    {
        globalBetCoinImage.DOKill();
        globalBetCoinImage.DOMove(jarTransform.position, duration).SetEase(Ease.InBack);

        yield return new WaitForSeconds(duration);

        // Coin arrived — clear the badge (text + visibility) for the next round,
        // and put the icon back at its resting spot so it's ready to show again.
        if (globalBetCoinRestCaptured) globalBetCoinImage.localPosition = globalBetCoinRestLocalPos;
        SetPooledBet(0);
    }


    static string FormatCoins(long v)
    {
        if (v >= 1_000_000) return $"{v / 1_000_000f:0.#}M";
        if (v >= 1_000)     return $"{v / 1_000f:0.#}k";
        return v.ToString();
    }
}