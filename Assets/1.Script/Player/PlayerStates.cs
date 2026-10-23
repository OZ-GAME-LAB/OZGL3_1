using UnityEngine;
using OZ.UI.Contracts;

public enum PlayerState
{
    Idle,
    Walk,
    Jump,   // 점프 시작 (도약)
    Air,    // 공중 (상승 + 낙하)
    Land,   // 착지
    Dash,   // 회피 대시 (짧은 무적)
    Attack,
    Skill, //Q/E/R 스킬 시전
    Ladder, // 사다리 매달림 (W/S)
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
        if (player.IsSkillRequested)
        {
            player.ChangeState(PlayerState.Skill);
            return true;
        }
        if (player.IsAttackRequested)
        {
            player.ChangeState(PlayerState.Attack);
            return true;
        }
        if (player.CanGrabLadder)
        {
            player.ChangeState(PlayerState.Ladder);
            return true;
        }
        if (player.IsJumpRequested)
        {
            // S를 누른 채 통과 발판 위에서 점프 -> 아랫점프
            if (player.TryDropThrough())
            {
                player.ChangeState(PlayerState.Air);
                return true;
            }
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
        //공중에서도 스킬 사용 가능
        if (player.IsSkillRequested)
        {
            player.ChangeState(PlayerState.Skill);
            return;
        }
        if (player.IsAttackRequested)
        {
            player.ChangeState(PlayerState.Attack);
            return;
        }
        if (player.CanGrabLadder)
        {
            player.ChangeState(PlayerState.Ladder);
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
    private IPlayerAttack _pendingAttack;
    private Vector3 _aimPoint;
    private float _contactAt;
    private bool _committed;
    
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
        _pendingAttack = attack;
        _aimPoint = aimPoint;
        _committed = false;
        if (attack != null)
        {
            _duration = attack.Duration;
        }
        else
        {
            _duration = player.AttackDuration;
        }
        var visual = player.GetComponent<ProtagonistAnimationDriver>();
        _contactAt = visual != null ? _duration * visual.AttackContactNormalized : 0f;
        if (_contactAt <= 0f) CommitAttack(player);
    }
    
    public override void UpdateState(PlayerController player)
    {
        if (!_committed && ElapsedTime >= _contactAt) CommitAttack(player);
        if (ElapsedTime >= _duration)
        {
            player.ChangeToDefaultState();
        }
    }
    private void CommitAttack(PlayerController player)
    {
        if (_committed) return;
        _committed = true;
        _pendingAttack?.Execute(player, _aimPoint);
    }
    public override void ExitState(PlayerController player)
    {
        _animator.ResetTrigger("Attack");
        _animator.SetBool("Attacking", false);   // 코드가 끝나면 애니메이션도 끝
    }
}

//스킬 시전 흐름(방향, 정지, 쿨다운 시작, 종료 타이밍)만 담당. 실제 판정은 PlayerSkillBase에 맡김.
public class PlayerState_Skill : PlayerState_Base
{
    private float _duration;
    public override void EnterState(PlayerController player)
    {
        base.EnterState(player);
        PlayerSkill skills = player.Skills;
        SkillSlot slot = player.RequestedSkillSlot;
        PlayerSkillBase skill = skills.GetEquipped(slot);
        // 입력~진입 사이에 장착·쿨다운이 바뀐 경우 대비 → 바로 빠져나감
        // (_duration = 0이면 다음 Update에서 즉시 기본 상태로 복귀)
        if (skill == null || !skills.CanUse(slot, out _))
        {
            _duration = 0f;
            return;
        }
        Vector3 aimPoint = player.Input.GetAimPoint(player.transform.position);
        player.LookAtImmediately(aimPoint.x - player.transform.position.x); // 커서 방향 바라보기
        if (player.IsGrounded)
        {
            player.Stop();
        }
        _animator.SetTrigger("Skill");
        // 쿨다운을 판정보다 먼저 거는 이유: Execute 안에서 에러가 나도 연타로 무한 발동되지 않게
        skills.StartCooldown(slot);
        skill.Execute(player, aimPoint, skills.GetFinalDamage(skill)); // 버프 포함 피해로 시전
        _duration = skill.Data.CastDuration;
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
        _animator.ResetTrigger("Skill");
    }
}

public class PlayerState_Ladder : PlayerState_Base
{
    public override void EnterState(PlayerController player)
    {
        base.EnterState(player);

        _rigidbody.useGravity = false;            // 안 끄면 손을 놓지 않아도 아래로 미끄러짐
        _rigidbody.linearVelocity = Vector3.zero; // 점프 중 잡았을 때 남은 속도로 튀지 않게
        player.ResetAirDash();                    // 사다리를 잡으면 땅을 밟은 것처럼 공중 대시 회복

        // 사다리 가장자리에서 잡아도 몸이 사다리 중앙에 붙어 보이게
        Ladder ladder = player.CurrentLadder;
        Vector3 pos = _rigidbody.position;
        pos.x = ladder.CenterX;

        float hangY = ladder.TopY - player.LadderTopOffset - 0.05f;
        if (pos.y > hangY)
        {
            pos.y = hangY;
        }
        _rigidbody.position = pos;
        player.FaceLadder();

        _animator.SetBool("Climb", true);
    }

    public override void UpdateState(PlayerController player)
    {
        Ladder ladder = player.CurrentLadder;

        // 트리거 밖으로 벗어남 (옆으로 밀렸거나 사다리가 사라짐)
        if (ladder == null)
        {
            player.ChangeToDefaultState();
            return;
        }

        // 뛰어내리기.
        if (player.IsJumpRequested)
        {
            player.BlockLadderGrab(); // W를 누른 채 점프하면 바로 다시 붙어버리는 것 방지
            player.ChangeState(PlayerState.Jump);
            return;
        }

        float inputY = player.Input.MoveInput.y;
        float feetY = player.transform.position.y;

        // 꼭대기 도착 -> 발판 위로 올려놓음
        if (inputY > 0f && feetY >= ladder.TopY - player.LadderTopOffset)
        {
            _rigidbody.position = ladder.TopExitPosition;
            _rigidbody.linearVelocity = Vector3.zero;
            player.ChangeState(PlayerState.Idle);
            return;
        }

        // 바닥 도착. 
        // 발판 모서리에 GroundCheck가 닿아 바로 내려지는 것 방지
        if (inputY < 0f && player.IsGrounded && feetY <= ladder.BottomY + 0.3f)
        {
            player.ChangeState(PlayerState.Idle);
        }
        _animator.SetFloat("ClimbSpeed", inputY);
    }

    public override void FixedUpdateState(PlayerController player)
    {
        float inputY = player.Input.MoveInput.y;
        _rigidbody.linearVelocity = new Vector3(0f, inputY * player.ClimbSpeed, 0f);
    }

    public override void ExitState(PlayerController player)
    {
        // 점프·피격·사망 등 어떤 이유로 나가도 중력이 꺼진 채 남지 않게
        _rigidbody.useGravity = true;
        _animator.SetBool("Climb", false);
        player.LookAtImmediately(player.FacingX);
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
