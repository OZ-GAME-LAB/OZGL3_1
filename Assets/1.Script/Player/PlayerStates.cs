using UnityEngine;

public enum PlayerState
{
    Idle,
    Walk,
    Jump,   // 점프 시작 (도약)
    Air,    // 공중 (상승 + 낙하)
    Land,   // 착지
    Dash,   // 회피 대시 (짧은 무적)
    Attack,
    Hit,
    Dead
}

public interface IPlayerState
{
    void EnterState(PlayerController player);
    void UpdateState(PlayerController player);
    void FixedUpdateState(PlayerController player);
    void ExitState(PlayerController player);
}

public abstract class PlayerState_Base : IPlayerState
{
    protected Animator _animator;
    protected Rigidbody _rigidbody;
    private float _enterTime;

    protected float ElapsedTime { get { return Time.time - _enterTime; } }

    public virtual void EnterState(PlayerController player)
    {
        if (_animator == null)
        {
            _animator = player.GetAnimator();
        }

        if (_rigidbody == null)
        {
            _rigidbody = player.GetRigidbody();
        }

        _enterTime = Time.time;
    }

    public virtual void UpdateState(PlayerController player) { }
    public virtual void FixedUpdateState(PlayerController player) { }
    public virtual void ExitState(PlayerController player) { }

    // 지상 상태(Idle, Walk, Land) 공통 전환. 전환했으면 true.
    protected bool TryGroundAction(PlayerController player)
    {
        if (!player.IsGrounded)
        {
            player.ChangeState(PlayerState.Air); // 발판에서 떨어짐 → 점프 시작 없이 바로 공중
            return true;
        }
        player.ResetAirDash();
        if (player.IsDashRequested && player.CanDash)
        {
            player.ChangeState(PlayerState.Dash); // 회피가 최우선
            return true;
        }
        if (player.IsAttackRequested)
        {
            player.ChangeState(PlayerState.Attack);
            return true;
        }
        if (player.IsJumpRequested)
        {
            player.ChangeState(PlayerState.Jump);
            return true;
        }

        return false;
    }
}

public class PlayerState_Idle : PlayerState_Base
{
    public override void UpdateState(PlayerController player)
    {
        if (TryGroundAction(player))
        {
            return;
        }

        if (player.Input.MoveInput.x != 0)
        {
            player.ChangeState(PlayerState.Walk);
        }
    }

    public override void FixedUpdateState(PlayerController player)
    {
        player.Stop();
    }
}

public class PlayerState_Walk : PlayerState_Base
{
    public override void EnterState(PlayerController player)
    {
        base.EnterState(player);
        _animator.SetBool("Walk", true);
    }

    public override void UpdateState(PlayerController player)
    {
        if (TryGroundAction(player))
        {
            return;
        }
        if (player.Input.MoveInput.x == 0)
        {
            player.ChangeState(PlayerState.Idle);
        }
    }

    public override void FixedUpdateState(PlayerController player)
    {
        player.Move(player.WalkSpeed);
    }

    public override void ExitState(PlayerController player)
    {
        _animator.SetBool("Walk", false);
    }
}

// 점프 시작. 점프력을 주고, 땅을 벗어나면 공중으로 넘어감.
public class PlayerState_Jump : PlayerState_Base
{
    private const float MaxTakeoffTime = 0.2f; // 천장에 막혀서 못 뜨는 경우 대비

    public override void EnterState(PlayerController player)
    {
        base.EnterState(player);
        player.Jump();
        _animator.SetTrigger("Jump");
    }

    public override void UpdateState(PlayerController player)
    {
        // 점프 직후 1~2프레임은 아직 땅 체크에 걸려 있으므로, 실제로 떠야 Air로 넘어감
        if (!player.IsGrounded)
        {
            player.ChangeState(PlayerState.Air);
            return;
        }
        if (ElapsedTime >= MaxTakeoffTime)
        {
            player.ChangeState(PlayerState.Idle);
        }
    }

    public override void FixedUpdateState(PlayerController player)
    {
        player.Move(player.JumpSpeed);
    }
}

// 공중 (상승 + 낙하 공용)
public class PlayerState_Air : PlayerState_Base
{
    public override void EnterState(PlayerController player)
    {
        base.EnterState(player);
        _animator.SetBool("Air", true);
    }

    public override void UpdateState(PlayerController player)
    {
        if (player.IsDashRequested && player.CanDash)
        {
            player.ChangeState(PlayerState.Dash);
            return;
        }
        if (player.IsAttackRequested)
        {
            player.ChangeState(PlayerState.Attack);
            return;
        }
        if (player.IsGrounded)
        {
            player.ChangeState(PlayerState.Land);
            return;
        }

        _animator.SetFloat("VelocityY", _rigidbody.linearVelocity.y);
    }

    public override void FixedUpdateState(PlayerController player)
    {
        player.Move(player.JumpSpeed);
    }

    public override void ExitState(PlayerController player)
    {
        _animator.SetBool("Air", false);
    }
}

// 착지. 짧게 멈췄다가 Idle로. 점프/공격 입력은 착지 모션을 끊고 바로 나감.
public class PlayerState_Land : PlayerState_Base
{
    public override void EnterState(PlayerController player)
    {
        base.EnterState(player);
        player.Stop();
        _animator.SetTrigger("Land");
    }

    public override void UpdateState(PlayerController player)
    {
        if (TryGroundAction(player))
        {
            return;
        }
        if (ElapsedTime >= player.LandDuration)
        {
            player.ChangeState(PlayerState.Idle);
        }
    }

    public override void FixedUpdateState(PlayerController player)
    {
        player.Stop();
    }
}

// 회피 대시. 입력 방향(없으면 바라보는 방향)으로 일정 시간 직선 이동, 앞부분 무적.
public class PlayerState_Dash : PlayerState_Base
{
    private float _dirX;

    public override void EnterState(PlayerController player)
    {
        base.EnterState(player);
        float inputX = player.Input.MoveInput.x;
        _dirX = Mathf.Abs(inputX) > 0.01f ? Mathf.Sign(inputX) : player.FacingX;

        player.LookAtImmediately(_dirX);
        player.StartDash();
        _rigidbody.useGravity = false; // 공중 대시 중 떨어지지 않게
        Vector3 velocity = _rigidbody.linearVelocity;
        velocity.y = 0f;
        _rigidbody.linearVelocity = velocity;

        _animator.SetBool("IsDashing", true);
    }

    public override void UpdateState(PlayerController player)
    {
        if (ElapsedTime >= player.DashDuration)
        {
            player.ChangeToDefaultState();
        }
    }
    public override void FixedUpdateState(PlayerController player)
    {
        Vector3 velocity = _rigidbody.linearVelocity;
        velocity.x = _dirX * player.DashSpeed;
        _rigidbody.linearVelocity = velocity;
    }
    public override void ExitState(PlayerController player)
    {
        _rigidbody.useGravity = true;
        player.Stop(); // 대시 관성 제거 (미끄러지는 느낌을 원하면 삭제)
        _animator.SetBool("IsDashing", false);
    }
}

// 공격 흐름(방향, 정지, 종료 타이밍)만 담당. 실제 판정은 IPlayerAttack에 맡김.
public class PlayerState_Attack : PlayerState_Base
{
    private float _duration;
    
    public override void EnterState(PlayerController player)
    {
        base.EnterState(player);
    
        Vector3 aimPoint = player.Input.GetAimPoint(player.transform.position);
        player.LookAtImmediately(aimPoint.x - player.transform.position.x); // 커서 방향 바라보기
    
        if (player.IsGrounded)
        {
            player.Stop();
        }
    
        _animator.SetTrigger("Attack");
        _animator.SetBool("Attacking", true);

        IPlayerAttack attack = player.Attack;
        if (attack != null)
        {
            attack.Execute(player, aimPoint);
            _duration = attack.Duration;
        }
        else
        {
            _duration = player.AttackDuration;
        }
    }
    
    public override void UpdateState(PlayerController player)
    {
        if (ElapsedTime >= _duration)
        {
            player.ChangeToDefaultState();
        }
    }
    public override void ExitState(PlayerController player)
    {
        _animator.ResetTrigger("Attack");
        _animator.SetBool("Attacking", false);   // 코드가 끝나면 애니메이션도 끝
    }
}

// 경직
public class PlayerState_Hit : PlayerState_Base
{
    public override void EnterState(PlayerController player)
    {
        base.EnterState(player);
        player.Stop();
        _animator.SetTrigger("Hit");
    }

    public override void UpdateState(PlayerController player)
    {
        if (ElapsedTime >= player.HitStunTime)
        {
            player.ChangeToDefaultState();
        }
    }
    public override void ExitState(PlayerController player)
    {
        _animator.ResetTrigger("Hit");
    }
}

// 사망. 입력을 받지 않고, Revive 전까지 다른 상태로 나가지 않음.
public class PlayerState_Dead : PlayerState_Base
{
    public override void EnterState(PlayerController player)
    {
        base.EnterState(player);
        player.Stop();
        _animator.SetTrigger("Dead");
    }

    public override void FixedUpdateState(PlayerController player)
    {
        player.Stop();
    }

    public override void ExitState(PlayerController player)
    {
        _animator.ResetTrigger("Dead");
    }
}