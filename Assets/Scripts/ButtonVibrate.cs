using UnityEngine;
using DG.Tweening;

public class ButtonVibrate : MonoBehaviour
{
    [Header("Vibrate Settings")]
    [SerializeField] private float interval = 3f;       // time between vibrations
    [SerializeField] private float shakeDuration = 0.3f;
    [SerializeField] private float shakeStrength = 10f;
    [SerializeField] private int shakeVibrato = 20;
    [SerializeField] private float shakeRandomness = 90f;

    private RectTransform rectTransform;
    private Vector3 originalPosition;
    private Tween loopTween;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        originalPosition = rectTransform.anchoredPosition;
    }

    private void OnEnable()
    {
        StartVibrateLoop();
    }

    private void OnDisable()
    {
        loopTween?.Kill();
        rectTransform.anchoredPosition = originalPosition;
    }

    private void StartVibrateLoop()
    {
        loopTween = DOVirtual.DelayedCall(interval, Vibrate)
            .SetLoops(-1); // repeats forever
    }

    private void Vibrate()
    {
        rectTransform.DOShakeAnchorPos(
            shakeDuration,
            shakeStrength,
            shakeVibrato,
            shakeRandomness,
            false,
            true
        ).OnComplete(() =>
        {
            rectTransform.anchoredPosition = originalPosition;
        });
    }
}