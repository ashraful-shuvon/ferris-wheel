using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Controls the Game Rules panel.
/// - "?" button opens it with a smooth fade + scale pop-in.
/// - "X" button closes it with a fade + scale pop-out.
///
/// Setup:
///   1. Attach this script to the Game Rules panel GameObject.
///   2. Drag the panel's CanvasGroup into canvasGroup (auto-added if missing).
///   3. Drag the panel's RectTransform into panelRect (auto-grabbed if missing).
///   4. Wire the "?" button OnClick → GameRulesPanel.Open()
///   5. Wire the "X" button OnClick → GameRulesPanel.Close()
/// </summary>
public class GameRulesPanel : MonoBehaviour
{
    [Header("References")]
    public CanvasGroup   canvasGroup;
    public RectTransform panelRect;

    [Header("Animation")]
    public float openDuration  = 0.35f;
    public Ease  openEase      = Ease.OutBack;
    public float closeDuration = 0.25f;
    public Ease  closeEase     = Ease.InBack;

    void Awake()
    {
        if (panelRect   == null) panelRect   = GetComponent<RectTransform>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // Start hidden.
        canvasGroup.alpha          = 0f;
        canvasGroup.interactable   = false;
        canvasGroup.blocksRaycasts = false;
        panelRect.localScale       = Vector3.zero;
        gameObject.SetActive(false);
    }

    /// <summary>Wire to the "?" button OnClick.</summary>
    public void Open()
    {
        gameObject.SetActive(true);

        panelRect.DOKill();
        canvasGroup.DOKill();

        panelRect.localScale = Vector3.zero;
        canvasGroup.alpha    = 0f;

        panelRect.DOScale(Vector3.one, openDuration).SetEase(openEase);
        canvasGroup.DOFade(1f, openDuration * 0.6f)
                   .OnComplete(() =>
                   {
                       canvasGroup.interactable   = true;
                       canvasGroup.blocksRaycasts = true;
                   });
    }

    /// <summary>Wire to the "X" button OnClick.</summary>
    public void Close()
    {
        canvasGroup.interactable   = false;
        canvasGroup.blocksRaycasts = false;

        panelRect.DOKill();
        canvasGroup.DOKill();

        panelRect.DOScale(Vector3.zero, closeDuration).SetEase(closeEase);
        canvasGroup.DOFade(0f, closeDuration)
                   .OnComplete(() => gameObject.SetActive(false));
    }
}
