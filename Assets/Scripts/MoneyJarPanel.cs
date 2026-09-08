using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// Controls the Money Jar panel.
/// - MoneyJarBtn opens it with a fade + scale pop-in (same as Game Rules).
/// - Close (X) button closes it with a fade + scale pop-out.
/// - Receive is wired now; reward claim will be implemented later.
/// </summary>
public class MoneyJarPanel : MonoBehaviour
{
    [Header("References")]
    public CanvasGroup   canvasGroup;
    public RectTransform panelRect;
    public Button        closeButton;
    public Button        receiveButton;

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

        canvasGroup.alpha          = 0f;
        canvasGroup.interactable   = false;
        canvasGroup.blocksRaycasts = false;
        panelRect.localScale       = Vector3.zero;
        gameObject.SetActive(false);
    }

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

    /// <summary>Placeholder — reward claim will be implemented later.</summary>
    public void OnReceiveClicked()
    {
    }
}
