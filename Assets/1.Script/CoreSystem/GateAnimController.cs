using UnityEngine;

public class GateAnimController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    private static readonly int IsActive = Animator.StringToHash("IsActive");

    public void SetActive(bool active)
    {
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
        if (animator == null || animator.runtimeAnimatorController == null) return;
        animator.SetBool(IsActive, active);
    }

    private void OnDisable()
    {
        SetActive(false);
    }
}
