using UnityEngine;

public enum EnemyAnimState
{
    Idle,
    Move,
    Attack,
    Damaged
}

[DisallowMultipleComponent]
public class EnemyAnimController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    public event System.Action AttackHit;

    private static readonly int IsIdle = Animator.StringToHash("IsIdle");
    private static readonly int IsMove = Animator.StringToHash("IsMove");
    private static readonly int IsAttack = Animator.StringToHash("IsAttack");
    private static readonly int IsDamaged = Animator.StringToHash("IsDamaged");

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
        if (animator == null)
            Debug.LogWarning("EnemyAnimController에 Animator를 연결하세요.", this);
    }

    private void OnEnable()
    {
        ResetAnimState();
    }

    public void ChangeAnimState(EnemyAnimState state)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;

        switch (state)
        {
            case EnemyAnimState.Idle:
                SetMovementState(true, false);
                break;
            case EnemyAnimState.Move:
                SetMovementState(false, true);
                break;
            case EnemyAnimState.Attack:
                animator.SetTrigger(IsAttack);
                break;
            case EnemyAnimState.Damaged:
                animator.SetTrigger(IsDamaged);
                break;
        }
    }

    // 공격 클립의 타격 프레임에 이 함수를 Animation Event로 지정합니다.
    public void OnAttackHit()
    {
        if (isActiveAndEnabled) AttackHit?.Invoke();
    }

    public void ResetAnimState()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;

        animator.ResetTrigger(IsAttack);
        animator.ResetTrigger(IsDamaged);
        SetMovementState(true, false);
    }

    private void SetMovementState(bool idle, bool move)
    {
        animator.SetBool(IsIdle, idle);
        animator.SetBool(IsMove, move);
    }
}
