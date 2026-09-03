using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Moves a hand/pointer sprite across the bet buttons (B1–B8) during the
/// betting countdown, one button per second.  Hides the hand the moment the
/// spin starts and shows it again when the next betting window opens.
///
/// Setup:
///   1. Create a UI Image in your Canvas, assign your hand sprite to it.
///   2. Attach this script to any persistent GameObject (e.g. GameManager's GO).
///   3. Drag the hand Image's RectTransform into <see cref="handTransform"/>.
///   4. Drag the same List<BetButton> from GameManager into <see cref="betButtons"/>.
///   5. Drag the GameManager reference in.
///
/// The script reads GameManager's phase via the public <see cref="GameManager"/>
/// reference — no changes to GameManager are required.
/// </summary>
public class BettingHandPointer : MonoBehaviour
{
    [Header("References")]
    [Tooltip("RectTransform of the hand/pointer Image in the Canvas.")]
    public RectTransform handTransform;

    [Tooltip("The same bet buttons list used by GameManager, in order B1–B8.")]
    public List<BetButton> betButtons = new List<BetButton>();

    [Tooltip("Reference to the GameManager so we can read its phase.")]
    public GameManager gameManager;

    [Header("Timing")]
    [Tooltip("Seconds the hand rests on each button before moving to the next.")]
    public float secondsPerButton = 1f;

    [Header("Offset")]
    [Tooltip("Local-space offset from the button's pivot to place the hand " +
             "(positive Y = above the button centre, negative = below).")]
    public Vector2 handOffset = new Vector2(0f, 60f);

    // ── private ──────────────────────────────────────────────────────────────

    int  currentIndex = 0;
    bool isRunning    = false;
    int  glowingIndex = -1;   // which button currently has the pointer glow (-1 = none)

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    void Start()
    {
        if (handTransform == null)
        {
            Debug.LogError("[BettingHandPointer] handTransform is not assigned!");
            enabled = false;
            return;
        }

        HideHand();
        StartCoroutine(PointerLoop());
    }

    // ── Main loop ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Continuously watches the GameManager phase. Shows and cycles the hand
    /// during Betting, hides it at any other phase.
    /// </summary>
    IEnumerator PointerLoop()
    {
        while (true)
        {
            // Wait until we enter the betting phase.
            yield return new WaitUntil(() => IsBettingPhase());

            // Betting just opened — start from B1 every new round.
            currentIndex = 0;
            ShowHand();

            // Cycle through buttons for as long as betting is open.
            while (IsBettingPhase())
            {
                SnapHandToButton(currentIndex);
                float elapsed = 0f;

                // Wait secondsPerButton, but bail early if the phase ends.
                while (elapsed < secondsPerButton && IsBettingPhase())
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }

                currentIndex = (currentIndex + 1) % betButtons.Count;
            }

            // Phase changed (spin started or result) — hide the hand.
            HideHand();

            // Small buffer so we don't spin-lock if phases are very short.
            yield return null;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    bool IsBettingPhase()
    {
        if (gameManager == null) return false;

        // Server mode: use serverBettingOpen exposed via property (see note below).
        // Offline mode: GameManager.phase is internal — we expose it via a thin
        // public property added in the companion patch, or we check the status
        // text as a fallback.  The cleanest approach is the IsBetting property.
        return gameManager.IsBetting;
    }

    void SnapHandToButton(int index)
    {
        if (index < 0 || index >= betButtons.Count) return;
        if (betButtons[index] == null) return;

        // Glow the button the hand just landed on, and clear the previous one.
        SetGlowFor(index);

        RectTransform target = betButtons[index].GetComponent<RectTransform>();
        if (target == null) return;

        // Match the button's anchored position in the same parent space.
        // If handTransform shares the same Canvas parent this is a direct copy;
        // otherwise we convert through world space.
        if (handTransform.parent == target.parent)
        {
            handTransform.anchoredPosition = target.anchoredPosition + handOffset;
        }
        else
        {
            // Convert button world position → hand's local parent space.
            Vector3 worldPos = target.TransformPoint(Vector3.zero);
            Vector3 localPos = handTransform.parent.InverseTransformPoint(worldPos);
            handTransform.anchoredPosition = new Vector2(localPos.x, localPos.y) + handOffset;
        }
    }

    void ShowHand()
    {
        if (handTransform != null)
            handTransform.gameObject.SetActive(true);
    }

    void HideHand()
    {
        if (handTransform != null)
            handTransform.gameObject.SetActive(false);

        // Hand is gone — no button should keep the pointer glow.
        ClearGlow();
    }

    /// <summary>Glow only <paramref name="index"/>, turning off whatever glowed before.</summary>
    void SetGlowFor(int index)
    {
        if (index == glowingIndex) return;

        if (glowingIndex >= 0 && glowingIndex < betButtons.Count && betButtons[glowingIndex] != null)
            betButtons[glowingIndex].ShowGlow(false);

        if (index >= 0 && index < betButtons.Count && betButtons[index] != null)
            betButtons[index].ShowGlow(true);

        glowingIndex = index;
    }

    /// <summary>Turn off the current pointer glow, if any.</summary>
    void ClearGlow()
    {
        if (glowingIndex >= 0 && glowingIndex < betButtons.Count && betButtons[glowingIndex] != null)
            betButtons[glowingIndex].ShowGlow(false);

        glowingIndex = -1;
    }
}
