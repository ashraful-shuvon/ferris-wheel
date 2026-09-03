using System.Collections;
using UnityEngine;
using DG.Tweening;

public class BettingStartBanner : MonoBehaviour
{
    [Header("References")]
    public CanvasGroup canvasGroup;
    public RectTransform bannerRect;

    [Header("Pop-in")]
    public float popInDuration  = 0.6f;
    public Ease  popInEase      = Ease.OutElastic;
    public float fadeInDuration = 0.2f;

    [Header("Hold then hide")]
    public float holdDuration   = 1.2f;

    [Header("Pop-out")]
    public float popOutDuration = 0.3f;
    public Ease  popOutEase     = Ease.InBack;

    Coroutine sequenceRoutine;

    void Awake()
    {
        Debug.Log("[BettingStartBanner] Awake called on: " + gameObject.name);

        if (bannerRect  == null) bannerRect  = GetComponent<RectTransform>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        canvasGroup.alpha     = 0f;
        bannerRect.localScale = Vector3.zero;
    }

    public void ShowBanner()
    {
        Debug.Log("[BettingStartBanner] ShowBanner() called.");

        // StartCoroutine fails on an inactive GameObject (the banner starts
        // disabled in the scene) — activate before running the sequence.
        if (!gameObject.activeSelf) gameObject.SetActive(true);

        if (sequenceRoutine != null) { StopCoroutine(sequenceRoutine); sequenceRoutine = null; }

        bannerRect.DOKill();
        canvasGroup.DOKill();

        canvasGroup.alpha     = 0f;
        bannerRect.localScale = Vector3.zero;

        sequenceRoutine = StartCoroutine(PlaySequence());
    }

    public void HideBanner()
    {
        Debug.Log("[BettingStartBanner] HideBanner() called.");

        // Nothing to hide (and StartCoroutine would throw) while inactive.
        if (!gameObject.activeInHierarchy) return;

        if (sequenceRoutine != null) { StopCoroutine(sequenceRoutine); sequenceRoutine = null; }

        bannerRect.DOKill();
        canvasGroup.DOKill();

        StartCoroutine(PlayHide());
    }

    IEnumerator PlaySequence()
    {
        Debug.Log("[BettingStartBanner] PlaySequence started.");

        bannerRect.DOScale(Vector3.one, popInDuration).SetEase(popInEase);
        canvasGroup.DOFade(1f, fadeInDuration);

        yield return new WaitForSeconds(popInDuration);
        Debug.Log("[BettingStartBanner] Pop-in done. Scale: " + bannerRect.localScale + " Alpha: " + canvasGroup.alpha);

        yield return new WaitForSeconds(holdDuration);

        yield return StartCoroutine(PlayHide());
    }

    IEnumerator PlayHide()
    {
        bannerRect.DOKill();
        canvasGroup.DOKill();

        bannerRect.DOScale(Vector3.zero, popOutDuration).SetEase(popOutEase);
        canvasGroup.DOFade(0f, popOutDuration);

        yield return new WaitForSeconds(popOutDuration);

        canvasGroup.alpha     = 0f;
        bannerRect.localScale = Vector3.zero;

        Debug.Log("[BettingStartBanner] Hidden.");
    }
}