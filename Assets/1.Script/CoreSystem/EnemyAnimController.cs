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
    private bool canPauseAnimation;

    private static readonly int IsIdle = Animator.StringToHash("IsIdle");
    private static readonly int IsMove = Animator.StringToHash("IsMove");
    private static readonly int IsAttack = Animator.StringToHash("IsAttack");
    private static readonly int IsDamaged = Animator.StringToHash("IsDamaged");
    private static readonly int AttackType = Animator.StringToHash("AttackType");

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

    private void OnDisable()
    {
        ResumeAnimation();
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
                ResumeAnimation();
                animator.SetTrigger(IsDamaged);
                break;
        }
    }

    // 보스용 Animator에서만 AttackType(Int)을 추가하면 공격별 전환을 사용할 수 있습니다.
    public void SetAttackType(int type)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash != AttackType || parameter.type != AnimatorControllerParameterType.Int) continue;
            animator.SetInteger(AttackType, type);
            return;
        }
    }

    // 공격 클립의 타격 프레임에 이 함수를 Animation Event로 지정합니다.
    public void OnAttackHit()
    {
        if (isActiveAndEnabled) AttackHit?.Invoke();
    }

    public void ResetAnimState()
    {
        ResumeAnimation();
        if (animator == null || animator.runtimeAnimatorController == null) return;

        animator.ResetTrigger(IsAttack);
        animator.ResetTrigger(IsDamaged);
        SetMovementState(true, false);
    }

    public void BeginAttackPause()
    {
        ResumeAnimation();
        canPauseAnimation = true;
    }

    // 칼을 들어 올린 프레임에 지정하는 Animation Event입니다.
    public void PauseAnimation()
    {
        if (canPauseAnimation && isActiveAndEnabled && animator != null)
            animator.speed = 0f;
    }

    public void ResumeAnimation()
    {
        canPauseAnimation = false;
        if (animator != null) animator.speed = 1f;
    }

    private void SetMovementState(bool idle, bool move)
    {
        animator.SetBool(IsIdle, idle);
        animator.SetBool(IsMove, move);
    }
}
