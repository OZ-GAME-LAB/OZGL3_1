using UnityEngine;




// 공격 확장 지점. 지금은 구현체 없음.
// 나중에 검술/마법 공격이나 스킬을 이 인터페이스로 만들어 플레이어에 붙이거나 SetAttack으로 교체.
public interface IPlayerAttack
{
    float Duration { get; }                                  // 공격 상태 유지 시간
    void Execute(PlayerController player, Vector3 aimPoint); // 실제 판정
}
