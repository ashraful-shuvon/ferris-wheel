using UnityEngine;

[RequireComponent(typeof(Animator))]
public class BearAnimator : MonoBehaviour
{
    Animator animator;

    static readonly int IdleTrigger = Animator.StringToHash("Idle");
    static readonly int SpinTrigger = Animator.StringToHash("Spin");
    static readonly int WinTrigger  = Animator.StringToHash("Win");

    void Awake()
    {
        animator = GetComponent<Animator>();

        if (animator == null)
            Debug.LogError("[BearAnimator] No Animator component found on " + gameObject.name +
                           ". Add an Animator component and assign a Controller.");
    }

    public void PlayIdle()
    {
        if (animator == null) { Debug.LogWarning("[BearAnimator] animator is null — skipping PlayIdle."); return; }
        animator.SetTrigger(IdleTrigger);
    }

    public void PlayEat()
    {
        if (animator == null) { Debug.LogWarning("[BearAnimator] animator is null — skipping PlayEat."); return; }
        animator.SetTrigger(SpinTrigger);
    }

    public void PlayWin()
    {
        if (animator == null) { Debug.LogWarning("[BearAnimator] animator is null — skipping PlayWin."); return; }
        animator.SetTrigger(WinTrigger);
    }
}