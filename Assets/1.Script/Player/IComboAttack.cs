using UnityEngine;

// 여러 타로 이어지는 공격(검 3타 등)용 인터페이스
// PlayerState_Attack은 기존처럼 IPlayerAttack만 알고,
// "콤보면 다음 타로 이어 줌"만 추가로 처리하면 되게 -> 주먹(1타)은 아무것도 안 바뀜
public interface IComboAttack : IPlayerAttack
{
    int CurrentStep { get; }           // 지금 몇 번째 타인지 (0부터). 애니메이터 ComboIndex로 그대로 넘김
    float ContactNormalized { get; }   // 이번 타의 판정 시점 (0~1). 타마다 칼이 닿는 순간이 다르기 때문
    AnimationClip CurrentClip { get; } // 이번 타 클립. 드라이버가 재생 속도(AttackSpeed)를 맞출 때 사용

    void BeginCombo();  // 공격 시작 시 1타로 되돌림
    bool TryAdvance();  // 다음 타로 넘어감. 마지막 타였으면 false
}
