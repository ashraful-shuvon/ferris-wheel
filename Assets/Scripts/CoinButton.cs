using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Attach this to each of the 5 coin/bet-amount buttons (50, 500, 1k, 5k, 10k).
/// Handles swapping between the normal sprite and the selected sprite, plus
/// a scale "pop" animation on select/deselect via DOTween.
/// GameManager calls SetSelected(true/false) — this script does not know about
/// the other 4 buttons, GameManager handles the "only one selected" logic.
/// </summary>
public class CoinButton : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("The coin value this button represents, e.g. 50, 500, 1000, 5000, 10000.")]
    public long amount;

    [Header("Sprites")]
    public Image  buttonImage;     // the Image component showing the coin sprite
    public Sprite normalSprite;
    public Sprite selectedSprite;

    [Header("Scale Animation")]
    [Tooltip("Scale multiplier when selected. 1.2 = 20% bigger.")]
    public float selectedScale = 1.2f;

    [Tooltip("How long the pop animation takes, in seconds.")]
    public float scaleDuration = 0.2f;

    [Tooltip("Easing curve for the pop. OutBack gives a slight overshoot 'bounce' feel.")]
    public Ease scaleEase = Ease.OutBack;

    bool   isSelected;
    Vector3 originalScale;

    void Awake()
    {
        if (buttonImage == null) buttonImage = GetComponent<Image>();
        originalScale = transform.localScale;
        SetSelected(false, instant: true);   // start unselected, no animation on boot
    }

    void OnDestroy()
    {
        // Always kill any in-flight tween tied to this transform to avoid
        // errors when the object is destroyed mid-animation.
        transform.DOKill();
    }

    /// <summary>
    /// Swap sprite and animate scale based on selection state. Called by GameManager.
    /// </summary>
    public void SetSelected(bool selected, bool instant = false)
    {
        isSelected = selected;

        if (buttonImage != null)
        {
            if (selected && selectedSprite != null)
                buttonImage.sprite = selectedSprite;
            else if (!selected && normalSprite != null)
                buttonImage.sprite = normalSprite;
        }

        Vector3 targetScale = selected ? originalScale * selectedScale : originalScale;

        transform.DOKill();   // cancel any tween already running on this transform

        if (instant)
            transform.localScale = targetScale;
        else
            transform.DOScale(targetScale, scaleDuration).SetEase(scaleEase);
    }

    public bool IsSelected => isSelected;
}