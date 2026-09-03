using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class CoinFlyEffect : MonoBehaviour
{
    [Header("Setup")]
    public Canvas parentCanvas;

    [Tooltip("Coins will be spawned as children of this transform. " +
             "If left empty, falls back to parentCanvas.")]
    public RectTransform coinSpawnParent;

    [Header("Burst Settings")]
    public int coinCount = 4;
    public float staggerDelay = 0.05f;
    public float flightDuration = 0.45f;
    public float arcHeight = 80f;
    public Vector2 coinSize = new Vector2(40f, 40f);
    public float spawnJitter = 15f;

    [Header("Jar Flow Settings")]
    public float jarFlightDuration = 0.6f;

    [Header("Spawn Under Button")]
    [Tooltip("If true, once the coin lands on the button it is reparented under " +
             "the button at coinSiblingIndex so it renders at the correct layer. " +
             "FruitImage = 0, Coin = 1, DimOverlay = 2, BrightOverlay = 3.")]
    public bool spawnUnderTargetButton = true;

    [Tooltip("Sibling index the coin is placed at inside the button on landing. " +
             "Set to 1 so it sits above FruitImage (0) but below DimOverlay/BrightOverlay.")]
    public int coinSiblingIndex = 1;

    private List<GameObject> activeCoinsOnButtons = new List<GameObject>();

    Transform SpawnParent => coinSpawnParent != null
                           ? (Transform)coinSpawnParent
                           : (Transform)parentCanvas.transform;

    public void PlayCoinFly(RectTransform sourceRect, RectTransform targetRect, Sprite sprite)
    {
        if (sourceRect == null || targetRect == null || sprite == null || parentCanvas == null)
        {
            Debug.LogWarning("[CoinFlyEffect] Missing reference.");
            return;
        }
        StartCoroutine(SpawnBurst(sourceRect, targetRect, sprite));
    }

    IEnumerator SpawnBurst(RectTransform sourceRect, RectTransform targetRect, Sprite sprite)
    {
        Vector3 sourceWorldPos = sourceRect.position;
        Vector3 targetWorldPos = targetRect.position;

        for (int i = 0; i < coinCount; i++)
        {
            SpawnSingleCoin(sourceWorldPos, targetWorldPos, targetRect, sprite);
            yield return new WaitForSeconds(staggerDelay);
        }
    }

    void SpawnSingleCoin(Vector3 sourceWorldPos, Vector3 targetWorldPos,
                         RectTransform targetRect, Sprite sprite)
    {
        // Always spawn under SpawnParent first so the coin is on top during flight.
        GameObject coinObj = new GameObject("FlyingCoin", typeof(RectTransform), typeof(Image));
        coinObj.transform.SetParent(SpawnParent, false);

        RectTransform rt = coinObj.GetComponent<RectTransform>();
        rt.sizeDelta = coinSize;
        rt.position  = sourceWorldPos + (Vector3)(Random.insideUnitCircle * spawnJitter);

        Image img = coinObj.GetComponent<Image>();
        img.sprite         = sprite;
        img.raycastTarget  = false;
        img.preserveAspect = true;

        float startScale = Random.Range(0.85f, 1.1f);
        rt.localScale = Vector3.one * startScale;

        Vector3 start = rt.position;
        Vector3 end   = targetWorldPos;
        Vector3 mid   = Vector3.Lerp(start, end, 0.5f) + Vector3.up * arcHeight;

        Vector3[] path = new Vector3[] { start, mid, end };

        Sequence seq = DOTween.Sequence();
        seq.Append(rt.DOPath(path, flightDuration, PathType.CatmullRom).SetEase(Ease.InOutSine));
        seq.Join(rt.DOScale(startScale * 0.6f, flightDuration).SetEase(Ease.InQuad));
        // Coins are a one-shot visual flourish now, not kept stacked on the
        // button for a later jar sweep — destroy on arrival instead of
        // reparenting under the button and tracking in activeCoinsOnButtons.
        seq.OnComplete(() => Destroy(coinObj));
    }

    public Coroutine FlowAllCoinsToJar(Transform jarTransform, float duration = -1f)
    {
        if (jarTransform == null) return null;
        float d = duration > 0f ? duration : jarFlightDuration;
        return StartCoroutine(FlowCoinsSequence(jarTransform, d));
    }

    IEnumerator FlowCoinsSequence(Transform jarTransform, float duration)
    {
        List<GameObject> coinsToFly = new List<GameObject>(activeCoinsOnButtons);
        activeCoinsOnButtons.Clear();

        foreach (var coin in coinsToFly)
        {
            if (coin == null) continue;

            RectTransform rt  = coin.GetComponent<RectTransform>();
            Image         img = coin.GetComponent<Image>();

            if (img != null) { Color c = img.color; c.a = 1f; img.color = c; }

            GameObject targetCoin = coin;
            rt.DOMove(jarTransform.position, duration)
              .SetEase(Ease.InBack)
              .OnComplete(() =>
              {
                  rt.DOScale(Vector3.zero, 0.1f)
                    .OnComplete(() => Destroy(targetCoin));
              });
        }

        yield return new WaitForSeconds(duration + 0.15f);
    }
}